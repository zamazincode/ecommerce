using Commerce.Api.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Api.Features.BackgroundJobs;

/// Periyodik veri temizliği. Faz 5'ten beri RefreshTokens hiç temizlenmiyordu.
public sealed class CleanupJobs(AppDbContext db, TimeProvider clock, ILogger<CleanupJobs> logger)
{
    /// Süresi dolalı en az 30 gün olan token'lar silinir. Tampon süre bilerek
    /// var: bir token süresi dolar dolmaz silinirse, "yeniden kullanım tespiti"
    /// (AuthService.RefreshAsync) için elde tutulan kısa geçmiş kaybolur.
    private const int BufferDays = 30;

    [AutomaticRetry(Attempts = 2)]
    public async Task PurgeExpiredRefreshTokensAsync()
    {
        var cutoff = clock.GetUtcNow().UtcDateTime.AddDays(-BufferDays);

        var deleted = await db.RefreshTokens
            .Where(t => t.ExpiresAt < cutoff)
            .ExecuteDeleteAsync();

        logger.LogInformation("Süresi dolmuş {Count} refresh token silindi.", deleted);
    }
}
