using Commerce.Api.Common.Exceptions;
using Commerce.Api.Features.Orders;
using Commerce.Api.Persistence;
using Commerce.Domain.Common;
using Commerce.Domain.Orders;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Api.Features.BackgroundJobs;

/// Faz 7'de stok sipariş ANINDA düşüyor; ödenmeyen sipariş stoğu sonsuza kadar
/// rezerve tutar (Faz 8'in bıraktığı borç). Bu job süresi dolan siparişi iptal
/// eder ve stoğu iade eder.
public sealed class OrderExpiryJobs(
    AppDbContext db, OrderService orders, TimeProvider clock, ILogger<OrderExpiryJobs> logger)
{
    public const int OdemeSuresiDakika = 30;

    [AutomaticRetry(Attempts = 1, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task CancelExpiredPendingOrdersAsync()
    {
        var cutoff = clock.GetUtcNow().UtcDateTime.AddMinutes(-OdemeSuresiDakika);

        var numaralar = await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Pending && o.CreatedAt < cutoff)
            // Parası çekilmiş ama tutar uyuşmazlığı yüzünden Pending bırakılmış
            // siparişe DOKUNMA (Faz 8) — elle inceleme bekliyor.
            .Where(o => !o.Payments.Any(p => p.Status == PaymentStatus.Succeeded))
            .OrderBy(o => o.Id)
            .Select(o => o.OrderNumber)
            .Take(200) // tek turda sınırsız iş yapma
            .ToListAsync();

        var iptal = 0;

        foreach (var no in numaralar)
        {
            try
            {
                // expectedCurrentStatus: araya bir ödeme callback'i girip siparişi
                // Paid yapmış olabilir — Paid->Cancelled de geçerli bir geçiş
                // olduğu için bu koruma olmadan ödenmiş bir sipariş iptal edilirdi.
                await orders.ChangeStatusAsync(no, OrderStatus.Cancelled,
                    expectedCurrentStatus: OrderStatus.Pending);
                iptal++;
            }
            catch (ConflictException)
            {
                logger.LogInformation(
                    "Sipariş {OrderNumber} artık Pending değil, iptal atlandı.", no);
            }
            catch (InvalidOrderStatusTransitionException)
            {
                logger.LogWarning(
                    "Sipariş {OrderNumber} için geçersiz durum geçişi, atlandı.", no);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sipariş {OrderNumber} iptal edilirken hata oluştu.", no);
            }
        }

        logger.LogInformation("Süresi dolan {Count} sipariş iptal edildi.", iptal);
    }
}
