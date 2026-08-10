using System.Net.Http.Headers;
using System.Net.Http.Json;
using Commerce.Api.Features.Auth.Dtos;
using Commerce.Api.Persistence;
using Commerce.Api.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Commerce.IntegrationTests.Infrastructure;

[Collection(DatabaseCollection.Name)]
public abstract class IntegrationTestBase(DatabaseFixture fixture) : IAsyncLifetime
{
    protected CustomWebApplicationFactory Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    public ValueTask InitializeAsync()
    {
        Factory = new CustomWebApplicationFactory(fixture.ConnectionString);
        Client = Factory.CreateClient();
        return ValueTask.CompletedTask;
    }

    /// Her testten SONRA çalışır. Temizlik testin bitiminde yapılır ki
    /// bir test kirli veri bırakınca sonraki test sebepsiz patlamasın.
    public async ValueTask DisposeAsync()
    {
        await fixture.ResetDatabaseAsync();
        Client.Dispose();
        await Factory.DisposeAsync();
    }

    /// Arrange ve Assert aşamalarında veritabanına doğrudan erişim.
    protected async Task ExecuteDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(db);
    }

    protected async Task<T> ExecuteDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db);
    }

    /// Birden fazla bağımlılığı olan servisleri (job sınıfları gibi) scope içinde
    /// çözüp çalıştırmak için.
    protected async Task ExecuteScopedAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        await action(scope.ServiceProvider);
    }

    protected async Task<T> ExecuteScopedAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    /// Kullanıcı oluşturur, giriş yapar, Client'a Bearer token'ı yerleştirir.
    /// Döndürdüğü değer: kullanıcının kimliği (Assert aşamasında lazım olur).
    protected async Task<Guid> AuthenticateAsync(
        string email = "musteri@test.com",
        string password = "Test1234",
        string role = AppRoles.Customer)
    {
        var userId = await CreateUserAsync(email, password, role);
        var token = await GetAccessTokenAsync(email, password);
        Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return userId;
    }

    protected async Task<Guid> CreateUserAsync(string email, string password, string role)
    {
        using var scope = Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        var roleManager = sp.GetRequiredService<RoleManager<ApplicationRole>>();
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new ApplicationRole { Name = role });

        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = "Test",
            LastName = "Kullanıcı",
            EmailConfirmed = true,
            // Kind=Utc olmak zorunda, yoksa Npgsql timestamptz yazarken patlar.
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                "Test kullanıcısı oluşturulamadı: " +
                string.Join(", ", result.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, role);
        return user.Id;
    }

    protected async Task<string> GetAccessTokenAsync(string email, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login",
            new { email, password });
        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.AccessToken;
    }

    protected void ClearAuthentication()
        => Client.DefaultRequestHeaders.Authorization = null;

    /// Callback/webhook 302 döndürüyor. Varsayılan test istemcisi yönlendirmeyi
    /// OTOMATİK takip eder (AllowAutoRedirect varsayılanı true) ve
    /// http://localhost:3000/... isteği TestServer'a düşüp 404 olur; durum kodu
    /// iddiaları sessizce yanlış nesneye bakar. Yönlendirmeyi görmek isteyen test
    /// bu istemciyi kullanır.
    protected HttpClient CreateNoRedirectClient()
        => Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// Testin kendi HttpClient'ını kirletmeden başka bir kullanıcı adına
    /// istek atmak için (örn. Customer token'ıyla admin endpoint'i denemek).
    protected async Task<HttpClient> CreateAuthenticatedClientAsync(
        string email, string password = "Test1234", string role = AppRoles.Customer)
    {
        await CreateUserAsync(email, password, role);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(email, password));
        return client;
    }
}