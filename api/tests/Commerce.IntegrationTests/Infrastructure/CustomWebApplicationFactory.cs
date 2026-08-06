using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
// ConfigureTestServices bu namespace'te. Mvc.Testing paketiyle geliyor
// ama using'i ayrı yazman gerekiyor — kolayca gözden kaçar.
using Microsoft.AspNetCore.TestHost;

namespace Commerce.IntegrationTests.Infrastructure;

/// API'yi BELLEKTE ayağa kaldırır ve normal bir HttpClient verir.
/// Gerçek pipeline (middleware, auth, endpoint, EF Core) çalışır; ağ katmanı yok.
public sealed class CustomWebApplicationFactory(string connectionString)
    : WebApplicationFactory<Program>
{
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

        builder.ConfigureTestServices(services =>
        {
            // Faz 8/9/10'da dış dünya sınırlarını buraya ekleyeceğiz:
            // services.RemoveAll<IEmailService>();
            // services.AddSingleton<IEmailService>(EmailService);
            // services.RemoveAll<IPaymentProvider>();
            // services.RemoveAll<IImageStorage>();
        });
    }
}