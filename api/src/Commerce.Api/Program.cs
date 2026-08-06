using Commerce.Api.Persistence;
using Commerce.Api.Persistence.Identity;
using Commerce.Api.Persistence.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

// AddDefaultTokenProviders() e-posta doğrulama / şifre sıfırlama token'larını
// üretmek için IDataProtectionProvider'a ihtiyaç duyar. Normalde bunu
// AddAuthentication() kaydeder — o Faz 5'te geleceği için şimdilik açıkça ekliyoruz.
// Faz 5'te AddAuthentication() gelince bu satır zararsız hâle gelir (idempotent).
builder.Services.AddDataProtection();

builder.Services
    .AddIdentityCore<ApplicationUser>()
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

var app = builder.Build();

// "dotnet run -- seed" ile çalıştırınca veriyi yükleyip çıkar.
if (args.Contains("seed"))
{
    using var scope = app.Services.CreateScope();
    await DatabaseSeeder.SeedAsync(scope.ServiceProvider);
    return;
}

app.MapGet("/", () => "Commerce.Api çalışıyor");

app.Run();

// Integration testlerin Program tipini görebilmesi için ZORUNLU.
// Minimal API'de en sık takılınan nokta budur.
public partial class Program;