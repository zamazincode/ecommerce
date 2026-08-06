using Commerce.Api.Persistence;
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
}