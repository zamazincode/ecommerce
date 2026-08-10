using Commerce.Api.Common.Email;
using Commerce.Api.Features.Auth;
using Commerce.Api.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Commerce.Api.Features.BackgroundJobs;

/// Sepete ürün ekleyen üye 24 saat sonra hâlâ sipariş vermediyse hatırlatma
/// maili gider. Misafir sepetleri kapsam dışı (Redis'te, e-posta adresi yok).
public sealed class CartReminderJobs(
    AppDbContext db, NotificationEmailSender sender, IOptions<WebAppSettings> web,
    TimeProvider clock, ILogger<CartReminderJobs> logger)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task SendReminderAsync(Guid userId)
    {
        // CartItem'da UserId YOK; Cart üzerinden gidiyoruz. CartItem'ın query
        // filter'ı (Product.DeletedAt == null) silinmiş ürünleri zaten eliyor.
        var cart = await db.Carts.AsNoTracking()
            .Include(c => c.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart is null || cart.Items.Count == 0)
        {
            logger.LogInformation("Sepet hatırlatma: {UserId} için sepet boş/yok, atlandı.", userId);
            return;
        }

        // Tekilleştirme (K6) — GÖNDERİMDEN ÖNCE, çünkü buradaki risk çift mail.
        // ReminderSentAt <= UpdatedAt: sepet o mailden sonra değişmediyse ikinci
        // hatırlatma hak edilmemiştir.
        var now = clock.GetUtcNow().UtcDateTime;
        var affected = await db.Carts
            .Where(c => c.Id == cart.Id &&
                        (c.ReminderSentAt == null || c.ReminderSentAt <= c.UpdatedAt))
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ReminderSentAt, now));

        if (affected == 0)
        {
            logger.LogInformation("Sepet hatırlatma: {UserId} için zaten gönderilmiş, atlandı.", userId);
            return;
        }

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Email, u.FirstName })
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(user?.Email))
        {
            logger.LogWarning("Sepet hatırlatma: {UserId} için kullanıcı e-postası yok.", userId);
            return;
        }

        var urunAdlari = cart.Items.Select(i => i.Product.Name).ToList();
        await sender.SendCartReminderAsync(
            user.Email, user.FirstName ?? "Müşteri", urunAdlari, $"{web.Value.BaseUrl}/sepet");
    }
}
