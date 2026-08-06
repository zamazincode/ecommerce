using Commerce.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace Commerce.IntegrationTests.Infrastructure;

/// Test süiti başlarken Docker'da TEK KULLANIMLIK gerçek bir Postgres
/// container'ı ayağa kaldırır, bitince siler.
///
/// Neden InMemory provider değil: InMemory foreign key kontrolü yapmaz,
/// unique constraint'i zorlamaz, SQL üretmez. Testin geçer, prod'da patlar.
public sealed class DatabaseFixture : IAsyncLifetime
{
    // İmaj yapıcıya geçiliyor. Parametresiz yapıcı + .WithImage() kullanımı
    // Testcontainers 4.x'te kullanımdan kaldırıldı (CS0618).
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("commerce_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    private Respawner _respawner = null!;
    private NpgsqlConnection _connection = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        _connection = new NpgsqlConnection(ConnectionString);
        await _connection.OpenAsync();

        // Şemayı bir kez kur — gerçek migration'ları çalıştırıyoruz.
        // Bu sayede migration'ların bozuk olduğunu testlerde de yakalarsın.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            // BUNU UNUTURSAN migration kaydı da silinir ve sonraki test şema bulamaz.
            TablesToIgnore = [new Respawn.Graph.Table("__EFMigrationsHistory")]
        });
    }

    /// Her testten sonra: tabloları boşaltır, şemayı korur.
    /// Respawn şemayı analiz eder, foreign key sırasını hesaplar,
    /// doğru sırayla DELETE atar.
    public Task ResetDatabaseAsync() => _respawner.ResetAsync(_connection);

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _container.DisposeAsync();
    }
}

/// Container'ı TÜM integration testleri paylaşsın diye.
/// Her test için ayrı container başlatırsan süit dakikalarca sürer.
[CollectionDefinition(Name)]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database";
}