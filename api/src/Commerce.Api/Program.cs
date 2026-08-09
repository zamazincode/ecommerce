using System.Threading.RateLimiting;
using Commerce.Api.Common.Email;
using Commerce.Api.Common.Handlers;
using Commerce.Api.Common.OpenApi;
using Commerce.Api.Features.Auth;
using Commerce.Api.Features.Cart;
using Commerce.Api.Features.Catalog;
using Commerce.Api.Features.Orders;
using Commerce.Api.Features.Search;
using Commerce.Api.Persistence;
using Commerce.Api.Persistence.Identity;
using Commerce.Api.Persistence.Seeding;
using Commerce.Api.Persistence.Seeding.Import;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Testing ortamında Redis/Seq gibi dış servisler yok — testcontainers sadece
// Postgres ayağa kaldırıyor. Bu bayrağı birkaç yerde kullanacağız.
var isTesting = builder.Environment.IsEnvironment("Testing");

// ─────────────────────────────────────────────────────────────
// Log
// ─────────────────────────────────────────────────────────────
builder.Services.AddSerilog((services, lc) =>
{
    lc.ReadFrom.Configuration(builder.Configuration)
      .ReadFrom.Services(services)
      .Enrich.FromLogContext()
      .WriteTo.Console();

    var seqUrl = builder.Configuration["Seq:ServerUrl"];
    if (!isTesting && !string.IsNullOrWhiteSpace(seqUrl))
        lc.WriteTo.Seq(seqUrl);
});

// ─────────────────────────────────────────────────────────────
// Veritabanı ve Identity
// ─────────────────────────────────────────────────────────────
var postgresConnection = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres tanımlı değil.");

builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(postgresConnection));

// AddDefaultTokenProviders() IDataProtectionProvider'a ihtiyaç duyar.
builder.Services.AddDataProtection();

builder.Services
    .AddIdentityCore<ApplicationUser>(o =>
    {
        o.Password.RequiredLength = 8;
        o.Password.RequireNonAlphanumeric = false;
        o.User.RequireUniqueEmail = true;
        o.SignIn.RequireConfirmedEmail = false;
    })
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ─────────────────────────────────────────────────────────────
// Cache — HybridCache: L1 bellek + L2 Redis
// ─────────────────────────────────────────────────────────────
// IDistributedCache — misafir sepetinin GERÇEK deposu, cache değil.
// AddHybridCache bunu KAYDETMİYOR (ölçüldü): bu satır olmadan Testing
// ortamında DistributedGuestCartStore çözülemez, her sepet isteği 500 olur.
// AddDistributedMemoryCache TryAdd kullanıyor, AddStackExchangeRedisCache düz
// AddSingleton — sıra ne olursa olsun Redis yapılandırılmışsa Redis kazanır.
builder.Services.AddDistributedMemoryCache();

if (!isTesting)
{
    var redisConnection = builder.Configuration.GetConnectionString("Redis");
    if (!string.IsNullOrWhiteSpace(redisConnection))
        builder.Services.AddStackExchangeRedisCache(o => o.Configuration = redisConnection);
}

builder.Services.AddHybridCache(o =>
{
    o.DefaultEntryOptions = new Microsoft.Extensions.Caching.Hybrid.HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(10),        // L2 (Redis)
        LocalCacheExpiration = TimeSpan.FromMinutes(2) // L1 (bellek)
    };
});

// ─────────────────────────────────────────────────────────────
// Hata yönetimi, validasyon, OpenAPI
// ─────────────────────────────────────────────────────────────
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);

builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

// ─────────────────────────────────────────────────────────────
// Kimlik doğrulama (Faz 5)
// ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection(JwtSettings.SectionName))
    .Validate(s => !string.IsNullOrWhiteSpace(s.Key) && s.Key.Length >= 32,
              "Jwt:Key en az 32 karakter olmalı.")
    .ValidateOnStart();

builder.Services.AddOptions<WebAppSettings>()
    .Bind(builder.Configuration.GetSection(WebAppSettings.SectionName));

var jwtSection = builder.Configuration.GetSection(JwtSettings.SectionName);
// Sabit yedek anahtar YOK (K3): eksikse burada dur, sessizce zayıf imzayla devam etme.
var jwtKey = jwtSection["Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException(
        "Jwt:Key tanımlı değil ya da 32 karakterden kısa. Geliştirmede: " +
        "dotnet user-secrets set \"Jwt:Key\" \"<en az 32 karakter>\" --project api/src/Commerce.Api");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // .NET'in uzun claim URI'lerine ÇEVİRME. Bu satır gidince sub/role
        // claim'leri yeniden adlandırılır, GetUserId() ve IsInRole() sessizce çöker.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,      // varsayılan 5 dk tolerans süre testlerini bozar
            NameClaimType = JwtClaims.Sub,
            RoleClaimType = JwtClaims.Role
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthPolicies.AdminOnly, p => p.RequireRole(AppRoles.Admin))
    .AddPolicy(AuthPolicies.CanManageProducts, p => p.RequireRole(AppRoles.Admin));

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IEmailService, ConsoleEmailService>();

// ─────────────────────────────────────────────────────────────
// Özellik servisleri (Faz 3)
// ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<CatalogService>();

// ─────────────────────────────────────────────────────────────
// Arama servisi (Faz 4)
// ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<ISearchService, PostgresSearchService>();

// ─────────────────────────────────────────────────────────────
// Sepet (Faz 6)
// ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<CartService>();
// Durumsuz sarmalayıcı; bağımlılıkları (IDistributedCache, ILogger) singleton.
builder.Services.AddSingleton<IGuestCartStore, DistributedGuestCartStore>();

// ─────────────────────────────────────────────────────────────
// Sipariş (Faz 7)
// ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<AddressService>();

// ─────────────────────────────────────────────────────────────
// CORS
// ─────────────────────────────────────────────────────────────
const string CorsPolicy = "DefaultCors";
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    // AllowAnyOrigin KULLANMA: kimlik bilgisi taşıyan isteklerde zaten çalışmaz.
    .AllowCredentials()));

// ─────────────────────────────────────────────────────────────
// Rate limiting
// ─────────────────────────────────────────────────────────────
if (!isTesting)
{
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Genel: IP başına dakikada 200 istek
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 200,
                    Window = TimeSpan.FromMinutes(1)
                }));

        // Auth endpoint'leri için sıkı politika (.RequireRateLimiting("auth")).
        // AddFixedWindowLimiter TEK bir limiter kurar — tüm istemciler aynı
        // kovayı paylaşır. IP'ye bölmek için AddPolicy + partition şart.
        options.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

        // Sepet: IP başına dakikada 60. Misafir sepeti tamamen anonim
        // yazılabiliyor (X-Guest-Id'yi istemci üretiyor) — ikinci katman.
        options.AddPolicy("cart", ctx => RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

        // Sipariş: IP başına dakikada 20. Kimlik doğrulanmış olsa da
        // sipariş oluşturma en pahalı yazma yolu (transaction + stok kilidi).
        options.AddPolicy("orders", ctx => RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
            }));
    });
}

// ─────────────────────────────────────────────────────────────
// Health check
// ─────────────────────────────────────────────────────────────
var health = builder.Services.AddHealthChecks()
    .AddNpgSql(postgresConnection, name: "postgres", tags: ["ready"]);

if (!isTesting)
{
    var redisConnection = builder.Configuration.GetConnectionString("Redis");
    if (!string.IsNullOrWhiteSpace(redisConnection))
        health.AddRedis(redisConnection, name: "redis", tags: ["ready"]);
}

// ═════════════════════════════════════════════════════════════
var app = builder.Build();
// ═════════════════════════════════════════════════════════════

// "dotnet run -- seed" — Bogus ile SAHTE veri. Boş veritabanı ve testler için.
if (args.Contains("seed"))
{
    using var scope = app.Services.CreateScope();
    await DatabaseSeeder.SeedAsync(scope.ServiceProvider);
    return;
}

// "dotnet run -- import [dosya] [--keep]" — GERÇEK veri (D&R xlsx).
// Varsayılan davranış katalogu temizleyip yeniden yüklemek; --keep verilirse
// mevcut ürünler korunur ve SKU üzerinden upsert yapılır.
if (args.Contains("import"))
{
    using var scope = app.Services.CreateScope();
    await CatalogImportCommand.RunAsync(scope.ServiceProvider, app.Environment, args);
    return;
}

if (!isTesting && string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Redis")))
    app.Logger.LogWarning(
        "Redis yapılandırılmamış. Misafir sepetleri uygulama belleğinde tutulacak: " +
        "uygulama yeniden başlayınca ve ikinci bir sunucuda sepetler kaybolur.");

// Sıra önemli: hata yakalayıcı EN ÜSTTE olmalı ki altındaki her şeyi sarsın.
app.UseExceptionHandler();

app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diag, http) =>
    {
        // Bu iki alan sayesinde Seq'te "şu kullanıcının şu isteğindeki tüm loglar"
        // diye filtreleyebiliyorsun.
        diag.Set("TraceId", http.TraceIdentifier);
        diag.Set("UserId", http.User.FindFirst(JwtClaims.Sub)?.Value);
    };
});

app.UseCors(CorsPolicy);

if (!isTesting)
    app.UseRateLimiter();

// Sıra kritik: UseAuthentication token'ı okuyup HttpContext.User'ı doldurur,
// UseAuthorization politikaları uygular. Ters yazılırsa HER istek 401 döner.
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                  // /openapi/v1.json
    app.MapScalarApiReference();       // /scalar/v1
}

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                error = e.Value.Exception?.Message
            })
        });
    }
});

app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();

// ─────────────────────────────────────────────────────────────
// Katalog endpoint'leri (Faz 3)
// ─────────────────────────────────────────────────────────────
app.MapProductEndpoints();
app.MapCategoryEndpoints();
app.MapCatalogEndpoints();

// ─────────────────────────────────────────────────────────────
// Arama endpoint'leri (Faz 4)
// ─────────────────────────────────────────────────────────────
app.MapSearchEndpoints();

// ─────────────────────────────────────────────────────────────
// Kimlik doğrulama endpoint'leri (Faz 5)
// ─────────────────────────────────────────────────────────────
app.MapAuthEndpoints();

// ─────────────────────────────────────────────────────────────
// Sepet endpoint'leri (Faz 6)
// ─────────────────────────────────────────────────────────────
app.MapCartEndpoints();

// ─────────────────────────────────────────────────────────────
// Sipariş ve adres endpoint'leri (Faz 7)
// ─────────────────────────────────────────────────────────────
app.MapOrderEndpoints();
app.MapAddressEndpoints();

app.Run();

// Integration testlerin Program tipini görebilmesi için ZORUNLU.
public partial class Program;