using Commerce.Api.Common.Email;
using Commerce.Api.Features.Auth;
using Commerce.Api.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Commerce.Api.Features.BackgroundJobs;

/// Sipariş onayı ve kargo bildirimi. Ödeme başarılı olduğunda / sipariş
/// kargoya verildiğinde kuyruğa atılır (K9 — daima transaction commit'inden SONRA).
public sealed class OrderNotificationJobs(
    AppDbContext db, NotificationEmailSender sender, IOptions<WebAppSettings> web,
    TimeProvider clock, ILogger<OrderNotificationJobs> logger)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task SendOrderConfirmationAsync(int orderId)
    {
        // Include(o => o.User) YOK: Order entity'sinde böyle bir navigation yok
        // (CLAUDE.md — domain entity'leri yalnızca Guid UserId tutar).
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null)
        {
            logger.LogWarning("Sipariş onay maili: {OrderId} bulunamadı.", orderId);
            return;
        }

        if (order.ConfirmationEmailSentAt is not null)
        {
            logger.LogInformation("Sipariş onay maili zaten gönderilmiş: {OrderNumber}", order.OrderNumber);
            return;
        }

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == order.UserId)
            .Select(u => new { u.Email, u.FirstName })
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(user?.Email))
        {
            logger.LogWarning("Sipariş onay maili: {OrderNumber} için kullanıcı e-postası yok.", order.OrderNumber);
            return;
        }

        var model = new OrderMailModel(
            order.OrderNumber,
            order.Items.OrderBy(i => i.Id)
                .Select(i => new OrderMailLine(
                    i.ProductNameSnapshot, i.Quantity, i.UnitPriceSnapshot * i.Quantity))
                .ToList(),
            order.SubTotal, order.DiscountAmount, order.ShippingCost, order.Total,
            $"{web.Value.BaseUrl}/hesabim/siparisler/{order.OrderNumber}");

        // İstisna YUTULMAZ — Hangfire retry etsin.
        await sender.SendOrderConfirmationAsync(user.Email, user.FirstName ?? "Müşteri", model);

        // Önce gönder, sonra damgala (K4): SMTP hatasında mailin hiç gitmemesi,
        // ikinci kez gitmesinden daha kötü.
        var affected = await db.Orders
            .Where(o => o.Id == orderId && o.ConfirmationEmailSentAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(
                o => o.ConfirmationEmailSentAt, clock.GetUtcNow().UtcDateTime));

        if (affected == 0)
            logger.LogWarning("Onay maili damgası yazılamadı. OrderId: {OrderId}", orderId);
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task SendShippedNotificationAsync(int orderId)
    {
        var order = await db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null)
        {
            logger.LogWarning("Kargo bildirimi maili: {OrderId} bulunamadı.", orderId);
            return;
        }

        if (order.ShippedEmailSentAt is not null)
        {
            logger.LogInformation("Kargo bildirimi maili zaten gönderilmiş: {OrderNumber}", order.OrderNumber);
            return;
        }

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == order.UserId)
            .Select(u => new { u.Email, u.FirstName })
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(user?.Email))
        {
            logger.LogWarning("Kargo bildirimi maili: {OrderNumber} için kullanıcı e-postası yok.", order.OrderNumber);
            return;
        }

        var siparisUrl = $"{web.Value.BaseUrl}/hesabim/siparisler/{order.OrderNumber}";

        await sender.SendShippedAsync(user.Email, user.FirstName ?? "Müşteri", order.OrderNumber, siparisUrl);

        var affected = await db.Orders
            .Where(o => o.Id == orderId && o.ShippedEmailSentAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(
                o => o.ShippedEmailSentAt, clock.GetUtcNow().UtcDateTime));

        if (affected == 0)
            logger.LogWarning("Kargo bildirimi damgası yazılamadı. OrderId: {OrderId}", orderId);
    }
}
