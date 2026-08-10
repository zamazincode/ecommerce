using System.Diagnostics;
using Commerce.Api.Common.Exceptions;
using Commerce.Api.Features.BackgroundJobs;
using Commerce.Api.Features.Payments.Dtos;
using Commerce.Api.Persistence;
using Commerce.Api.Persistence.Identity;
using Commerce.Domain.Common;
using Commerce.Domain.Orders;
using Commerce.Domain.Pricing;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Commerce.Api.Features.Payments;

public sealed class PaymentService(
    AppDbContext db,
    IPaymentProvider provider,
    UserManager<ApplicationUser> userManager,
    IBackgroundJobClient backgroundJobs,
    IOptions<PaymentSettings> paymentSettings,
    TimeProvider clock,
    ILogger<PaymentService> logger)
{
    // ─────────────────────────────────────────────────────────
    // 1–2. Ödemeyi başlat
    // ─────────────────────────────────────────────────────────
    public async Task<PaymentInitializedDto> InitializeAsync(
        Guid userId, string orderNumber, string clientIp, CancellationToken ct = default)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, ct)
            ?? throw NotFoundException.For("Sipariş", orderNumber);

        // Admin muafiyeti yok — başkası adına ödeme başlatmanın anlamı yok.
        if (order.UserId != userId)
            throw new ForbiddenException("Bu sipariş için ödeme başlatamazsınız.");

        if (order.Status != OrderStatus.Pending)
            throw new BusinessRuleException(
                $"'{order.Status}' durumundaki bir sipariş için ödeme başlatılamaz.");

        // iyzico sıfır tutarlı işlemi reddeder (%100 kupon senaryosu, K10).
        if (order.Total <= 0m)
            throw new BusinessRuleException("Bu sipariş için ödeme alınmasına gerek yok.");

        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw NotFoundException.For("Kullanıcı", userId);

        var items = BuildBasketItems(order);

        var request = new PaymentRequest(
            OrderNumber: order.OrderNumber,
            Amount: order.Total,
            Buyer: new PaymentBuyer(
                Id: user.Id.ToString(),
                FirstName: user.FirstName ?? "Müşteri",
                LastName: user.LastName ?? "-",
                Email: user.Email!,
                Phone: order.ShippingAddress.Phone,
                City: order.ShippingAddress.City,
                Country: "Turkey",
                Address: order.ShippingAddress.FullAddress,
                Ip: clientIp),
            Items: items,
            CallbackUrl: paymentSettings.Value.CallbackUrl);

        var result = await provider.InitializeAsync(request, ct);

        // Ham cevabı HER DURUMDA sakla — başarısızlıkta da. "Sağlayıcı tam
        // olarak ne dedi?" sorusunu cevaplayabilmek çok değerli.
        // Bilinçli kabul: burada uygulama çökerse iyzico'da form oluşmuş,
        // bizde kayıt yoktur. Form kullanıcıya hiç ulaşmadığı için kart da
        // çekilemez — risk yok.
        var payment = new Payment
        {
            OrderId = order.Id,
            Provider = provider.Name,
            ProviderReference = result.ProviderReference,
            Status = result.Success ? PaymentStatus.Pending : PaymentStatus.Failed,
            Amount = order.Total,
            RawResponse = Truncate(result.RawResponse),
            CreatedAt = clock.GetUtcNow().UtcDateTime
        };

        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct);

        if (!result.Success)
            throw new BusinessRuleException(result.ErrorMessage ?? "Ödeme başlatılamadı.");

        return new PaymentInitializedDto(
            order.OrderNumber, order.Total, result.ProviderReference!, result.CheckoutContent!);
    }

    /// K1: iyzico basketItems toplamının price'a TAM eşit olmasını zorunlu
    /// tutar. Sipariş toplamı SubTotal - Discount + ShippingCost olduğu için
    /// satırların (UnitPriceSnapshot × Quantity) toplamı Total'a EŞİT DEĞİL —
    /// AmountAllocator farkı orana göre satırlara dağıtır.
    private static List<PaymentBasketItem> BuildBasketItems(Order order)
    {
        var lines = order.Items.OrderBy(i => i.Id).ToList();

        // Her satıra en az 1 kuruş düşemiyorsa tek kaleme indirgenir.
        if (order.Total < 0.01m * lines.Count)
        {
            var single = new List<PaymentBasketItem>
            {
                new("order", $"Sipariş {order.OrderNumber}", "Ürün", order.Total)
            };
            return single;
        }

        var weights = lines.Select(i => i.UnitPriceSnapshot * i.Quantity).ToList();
        var allocated = AmountAllocator.Distribute(weights, order.Total);

        var items = lines.Zip(allocated, (line, price) =>
                new PaymentBasketItem(line.ProductId.ToString(), line.ProductNameSnapshot, "Ürün", price))
            .ToList();

        var itemsTotal = items.Sum(i => i.Price);
        Debug.Assert(itemsTotal == order.Total, "Sepet kalemleri toplamı sipariş toplamına eşit değil.");
        if (itemsTotal != order.Total)
            throw new InvalidOperationException(
                $"Sepet kalemleri toplamı ({itemsTotal}) sipariş toplamına ({order.Total}) eşit değil.");

        return items;
    }

    // ─────────────────────────────────────────────────────────
    // 5–7. Callback / webhook — ikisi de buraya iniyor
    // ─────────────────────────────────────────────────────────
    public async Task<PaymentCompletionResult> CompleteAsync(
        string providerReference, CancellationToken ct = default)
    {
        // ① YEREL kaydı ara. Bulunamazsa sağlayıcıya HİÇ SORMA — anonim
        // endpoint'i amplifikasyon saldırısına açık bırakmayalım (K2).
        var payment = await db.Payments
            .AsNoTracking()
            .Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.ProviderReference == providerReference, ct);

        if (payment is null)
        {
            logger.LogWarning(
                "Bilinmeyen ödeme referansı ile callback/webhook geldi: {Reference}", providerReference);
            return new PaymentCompletionResult(PaymentCompletionOutcome.UnknownReference, null);
        }

        // ② Zaten Succeeded ise sağlayıcıya SORMA — idempotency kısa devresi,
        // hem gereksiz ağ gidiş-dönüşünü hem de sağlayıcının rate limitini
        // tasarruf ediyor.
        if (payment.Status == PaymentStatus.Succeeded)
        {
            logger.LogInformation(
                "Ödeme zaten tamamlanmış, tekrar işlenmedi. Referans: {Reference}", providerReference);
            return new PaymentCompletionResult(
                PaymentCompletionOutcome.Succeeded, await ReadStatusAsync(payment.OrderId, ct));
        }

        // ③ SAĞLAYICIYA SOR — transaction DIŞINDA, HTTP çağrısı transaction
        // içinde olmasın.
        var verification = await provider.VerifyAsync(providerReference, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var raw = Truncate(verification.RawResponse);

        if (!verification.IsPaid)
        {
            await db.Payments
                .Where(p => p.Id == payment.Id && p.Status != PaymentStatus.Succeeded)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Status, PaymentStatus.Failed)
                    .SetProperty(p => p.RawResponse, raw)
                    .SetProperty(p => p.UpdatedAt, now), ct);

            logger.LogWarning(
                "Ödeme başarısız. Sipariş: {OrderNumber}, Hata: {Error}",
                payment.Order.OrderNumber, verification.ErrorMessage);

            // Sipariş Pending kalır, stok rezerve kalır — kullanıcı tekrar dener.
            return new PaymentCompletionResult(
                PaymentCompletionOutcome.Failed, await ReadStatusAsync(payment.OrderId, ct));
        }

        if (verification.PaidAmount != payment.Order.Total)
        {
            // Para GERÇEKTEN çekildi — Failed YAZMA, muhasebeyi yalanlarsın ve
            // ProviderTransactionId'yi (iade için gereken) çöpe atarsın.
            logger.LogError(
                "Tutar uyuşmazlığı! Sipariş: {OrderNumber}, beklenen: {Expected}, tahsil edilen: {Actual}",
                payment.Order.OrderNumber, payment.Order.Total, verification.PaidAmount);

            await db.Payments
                .Where(p => p.Id == payment.Id && p.Status != PaymentStatus.Succeeded)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Status, PaymentStatus.Succeeded)
                    .SetProperty(p => p.ProviderTransactionId, verification.ProviderTransactionId)
                    .SetProperty(p => p.RawResponse, raw)
                    .SetProperty(p => p.UpdatedAt, now), ct);

            // Sipariş DEĞİŞTİRİLMEZ — elle inceleme bekliyor.
            return new PaymentCompletionResult(
                PaymentCompletionOutcome.AmountMismatch, await ReadStatusAsync(payment.OrderId, ct));
        }

        // BAŞARILI — sipariş Paid'e taşınacak.
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Durum makinesi son sözü söyler; yazma işini koşullu ExecuteUpdateAsync
        // devralıyor (K5 — OrderService.ChangeStatusAsync kendi transaction'ını
        // açtığı için buradan çağrılamaz).
        if (payment.Order.Status == OrderStatus.Pending)
            OrderStatusTransition.EnsureCanTransition(payment.Order.Status, OrderStatus.Paid);

        var paymentAffected = await db.Payments
            .Where(p => p.Id == payment.Id && p.Status != PaymentStatus.Succeeded)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, PaymentStatus.Succeeded)
                .SetProperty(p => p.ProviderTransactionId, verification.ProviderTransactionId)
                .SetProperty(p => p.RawResponse, raw)
                .SetProperty(p => p.UpdatedAt, now), ct);

        if (paymentAffected == 0)
        {
            // Eşzamanlı bir callback bizden önce yarışı kazandı — sessizce
            // mevcut durumu dön.
            await tx.CommitAsync(ct);
            return new PaymentCompletionResult(
                PaymentCompletionOutcome.Succeeded, await ReadStatusAsync(payment.OrderId, ct));
        }

        var orderAffected = await db.Orders
            .Where(o => o.Id == payment.OrderId && o.Status == OrderStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OrderStatus.Paid)
                .SetProperty(o => o.UpdatedAt, now), ct);

        var outcome = PaymentCompletionOutcome.Succeeded;

        if (orderAffected == 0)
        {
            // Sipariş Pending değildi — K6: mükerrer ödeme ya da iptal edilmiş
            // siparişe geç kalmış ödeme. İki durumda da rollback YOK,
            // ProviderTransactionId mutlaka diske yazılır.
            var currentOrderStatus = await db.Orders
                .AsNoTracking()
                .Where(o => o.Id == payment.OrderId)
                .Select(o => o.Status)
                .FirstAsync(ct);

            if (currentOrderStatus == OrderStatus.Paid)
            {
                logger.LogError(
                    "Mükerrer başarılı ödeme — iade gerekebilir. Sipariş: {OrderNumber}",
                    payment.Order.OrderNumber);
            }
            else
            {
                logger.LogError(
                    "Ödeme başarılı ama sipariş {OrderNumber} '{Status}' durumunda — elle inceleyin.",
                    payment.Order.OrderNumber, currentOrderStatus);
                outcome = PaymentCompletionOutcome.AmountMismatch;
            }
        }

        await tx.CommitAsync(ct);

        logger.LogInformation(
            "Ödeme tamamlandı. Sipariş: {OrderNumber}, işlem: {TransactionId}",
            payment.Order.OrderNumber, verification.ProviderTransactionId);

        // Enqueue daima commit'ten SONRA (K9). orderAffected > 0 koşulu "siparişi
        // Paid'e taşıyan biz olduk" demek; mükerrer callback ikinci job'ı üretmez
        // (job zaten idempotent — ConfirmationEmailSentAt — bu ikinci kemer).
        if (outcome == PaymentCompletionOutcome.Succeeded && orderAffected > 0)
            backgroundJobs.Enqueue<OrderNotificationJobs>(j => j.SendOrderConfirmationAsync(payment.OrderId));

        return new PaymentCompletionResult(outcome, await ReadStatusAsync(payment.OrderId, ct));
    }

    // ─────────────────────────────────────────────────────────
    // Durum sorgusu
    // ─────────────────────────────────────────────────────────
    public async Task<PaymentStatusDto> GetStatusAsync(
        Guid userId, string orderNumber, bool isAdmin, CancellationToken ct = default)
    {
        var order = await db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, ct)
            ?? throw NotFoundException.For("Sipariş", orderNumber);

        if (!isAdmin && order.UserId != userId)
            throw new ForbiddenException("Bu siparişe erişim yetkiniz yok.");

        var payment = await db.Payments
            .AsNoTracking()
            .Where(p => p.OrderId == order.Id)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);

        return new PaymentStatusDto(
            order.OrderNumber, order.Status, payment?.Status, payment?.Amount ?? order.Total, payment?.UpdatedAt);
    }

    // ─────────────────────────────────────────────────────────
    // Yardımcılar
    // ─────────────────────────────────────────────────────────
    /// ExecuteUpdateAsync change tracker'ı atlıyor — okumalar bu yüzden hep
    /// TAZE bir sorgudan (AsNoTracking) üretiliyor, karışık takipli/takipsiz
    /// durum bırakılmıyor.
    private async Task<PaymentStatusDto> ReadStatusAsync(int orderId, CancellationToken ct)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new { o.OrderNumber, o.Status, o.Total })
            .FirstAsync(ct);

        var payment = await db.Payments
            .AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);

        return new PaymentStatusDto(
            order.OrderNumber, order.Status, payment?.Status, payment?.Amount ?? order.Total, payment?.UpdatedAt);
    }

    /// RawResponse kolonu 20.000 karakter. K8'den sonra pratikte devreye
    /// girmez (yalnızca tanılama alanları serileştiriliyor) — emniyet kemeri.
    private static string Truncate(string value)
        => value.Length <= 20_000 ? value : value[..20_000];
}
