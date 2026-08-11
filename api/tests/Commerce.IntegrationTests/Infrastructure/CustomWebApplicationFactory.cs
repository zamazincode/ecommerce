using Commerce.Api.Common.Email;
using Commerce.Api.Common.Images;
using Commerce.Api.Features.Payments;
using Hangfire;
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
    public FakePaymentProvider PaymentProvider { get; } = new();
    public RecordingBackgroundJobClient BackgroundJobs { get; } = new();
    public FakeImageStorage ImageStorage { get; } = new();

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

        // ProductImageUrls cloud adını buradan alır; testler URL'i BİLDİKLERİ bir
        // değere göre iddia edebilsin (yoksa FallbackCloudName'e düşer).
        builder.UseSetting("Cloudinary:CloudName", "test-cloud");

        builder.ConfigureTestServices(services =>
        {
            // DIŞ DÜNYA SINIRI: gerçek mail gitmesin, ama "gitti mi" diye sorabilelim.
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(EmailService);

            // DIŞ DÜNYA SINIRI: gerçek iyzico çağrısı gitmesin, ama "ne gönderdik /
            // kaç kez sorduk" diye sorabilelim.
            services.RemoveAll<IPaymentProvider>();
            services.AddSingleton<IPaymentProvider>(PaymentProvider);

            // DIŞ DÜNYA SINIRI: gerçek kuyruğa yazma, ama "ne kuyruğa girdi" diye sorabil.
            services.RemoveAll<IBackgroundJobClient>();
            services.AddSingleton<IBackgroundJobClient>(BackgroundJobs);

            // DIŞ DÜNYA SINIRI: gerçek Cloudinary silme çağrısı gitmesin, ama "ne
            // silindi" diye sorabilelim. Program.cs Testing'de zaten FakeImageStorage
            // kaydediyor; burada testin elindeki ÖRNEKLE değiştiriyoruz.
            services.RemoveAll<IImageStorage>();
            services.AddSingleton<IImageStorage>(ImageStorage);
        });
    }
}
