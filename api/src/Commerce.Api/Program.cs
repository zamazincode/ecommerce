using System.Security.Claims;
using System.Threading.RateLimiting;
using Commerce.Api.Common.Handlers;
using Commerce.Api.Features.Catalog;
using Commerce.Api.Persistence;
using Commerce.Api.Persistence.Identity;
using Commerce.Api.Persistence.Seeding;
using Commerce.Api.Persistence.Seeding.Import;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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

builder.Services.AddOpenApi();

// ─────────────────────────────────────────────────────────────
// Özellik servisleri (Faz 3)
// ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<CatalogService>();

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

        // Auth endpoint'leri için sıkı politika
        // .RequireRateLimiting("auth")
        options.AddFixedWindowLimiter("auth", o =>
        {
            o.PermitLimit = 5;
            o.Window = TimeSpan.FromMinutes(1);
            o.QueueLimit = 0;
        });
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

// Sıra önemli: hata yakalayıcı EN ÜSTTE olmalı ki altındaki her şeyi sarsın.
app.UseExceptionHandler();

app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diag, http) =>
    {
        // Bu iki alan sayesinde Seq'te "şu kullanıcının şu isteğindeki tüm loglar"
        // diye filtreleyebiliyorsun.
        diag.Set("TraceId", http.TraceIdentifier);
        diag.Set("UserId", http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    };
});

app.UseCors(CorsPolicy);

if (!isTesting)
    app.UseRateLimiter();

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



app.Run();

// Integration testlerin Program tipini görebilmesi için ZORUNLU.
public partial class Program;