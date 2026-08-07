using Commerce.Api.Common.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
// ConfigureTestServices bu namespace'te. Mvc.Testing paketiyle geliyor
// ama using'i ayrı yazman gerekiyor — kolayca gözden kaçar.
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Commerce.IntegrationTests.Infrastructure;

/// API'yi BELLEKTE ayağa kaldırır ve normal bir HttpClient verir.
/// Gerçek pipeline (middleware, auth, endpoint, EF Core) çalışır; ağ katmanı yok.
public sealed class CustomWebApplicationFactory(string connectionString)
    : WebApplicationFactory<Program>
{
    public FakeEmailService EmailService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Bağlantı dizesini DI'a girmeden, YAPILANDIRMA seviyesinde değiştiriyoruz.
        // Program.cs zaten Configuration.GetConnectionString("Postgres") okuyor.
        //
        // Neden RemoveAll<DbContextOptions<AppDbContext>>() değil:
        // EF Core sürümleri arasında DI kayıtları değişiyor (IDbContextOptionsConfiguration
        // gibi yeni tipler eklendi). UseSetting sürümden bağımsız çalışır ve
        // çok daha az kırılgandır.
        builder.UseSetting("ConnectionStrings:Postgres", connectionString);

        // Testlerde sabit, bilinen bir imza anahtarı. appsettings.json (Testing'de
        // okunan dosya) Jwt bölümü içermiyor — Issuer/Audience de burada verilmeli,
        // yoksa ValidIssuer/ValidAudience null kalır ve her token reddedilir.
        builder.UseSetting("Jwt:Key", "test-imza-anahtari-en-az-32-karakter-uzunlugunda");
        builder.UseSetting("Jwt:Issuer", "commerce-api");
        builder.UseSetting("Jwt:Audience", "commerce-clients");

        builder.ConfigureTestServices(services =>
        {
            // DIŞ DÜNYA SINIRI: gerçek mail gitmesin, ama "gitti mi" diye sorabilelim.
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(EmailService);

            // Faz 8/10'da: IPaymentProvider, IImageStorage
        });
    }
}
