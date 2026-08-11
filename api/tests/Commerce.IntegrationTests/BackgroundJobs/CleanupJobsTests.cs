using Commerce.Api.Features.BackgroundJobs;
using Commerce.Api.Persistence.Identity;
using Commerce.Domain.Users;
using Commerce.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Commerce.IntegrationTests.BackgroundJobs;

public class CleanupJobsTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task RunPurgeAsync()
        => ExecuteScopedAsync(sp =>
            sp.GetRequiredService<CleanupJobs>().PurgeExpiredRefreshTokensAsync());

    [Fact]
    public async Task PurgeExpiredRefreshTokens_RemovesOldTokens_KeepsRecentOnes()
    {
        var userId = await CreateUserAsync("musteri@test.com", "Test1234", AppRoles.Customer);
        var now = DateTime.UtcNow;

        await ExecuteDbAsync(async db =>
        {
            // 40 gün önce süresi dolmuş — 30 günlük tampon aşıldı, silinmeli.
            db.RefreshTokens.Add(new RefreshToken
            {
                UserId = userId,
                Token = "eski-token-hash",
                ExpiresAt = now.AddDays(-40),
                CreatedAt = now.AddDays(-70)
            });
            // Hâlâ geçerli — kalmalı.
            db.RefreshTokens.Add(new RefreshToken
            {
                UserId = userId,
                Token = "guncel-token-hash",
                ExpiresAt = now.AddDays(20),
                CreatedAt = now.AddDays(-10)
            });
            await db.SaveChangesAsync();
        });

        await RunPurgeAsync();

        var kalanlar = await ExecuteDbAsync(db => db.RefreshTokens.ToListAsync(Ct));
        kalanlar.Count.ShouldBe(1);
        kalanlar[0].Token.ShouldBe("guncel-token-hash");
    }

    [Fact]
    public async Task PurgeExpiredRefreshTokens_KeepsRevokedTokenYoungerThan30Days()
    {
        var userId = await CreateUserAsync("musteri@test.com", "Test1234", AppRoles.Customer);
        var now = DateTime.UtcNow;

        await ExecuteDbAsync(async db =>
        {
            // 5 gün önce iptal edildi (rotasyon ya da reuse-detection) ama
            // doğal süresi hâlâ ileride — silinirse yeniden kullanım tespiti
            // için elde tutulan kısa geçmiş kaybolur.
            db.RefreshTokens.Add(new RefreshToken
            {
                UserId = userId,
                Token = "iptal-edilmis-token-hash",
                ExpiresAt = now.AddDays(25),
                CreatedAt = now.AddDays(-5),
                RevokedAt = now.AddDays(-5)
            });
            await db.SaveChangesAsync();
        });

        await RunPurgeAsync();

        var kalanlar = await ExecuteDbAsync(db => db.RefreshTokens.ToListAsync(Ct));
        kalanlar.Count.ShouldBe(1);
    }

    [Fact]
    public async Task PurgeExpiredRefreshTokens_OnEmptyTable_DoesNotThrow()
        => await Should.NotThrowAsync(RunPurgeAsync);
}
