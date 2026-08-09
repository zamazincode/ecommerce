using Commerce.Api.Common.Exceptions;
using Commerce.Api.Common.Extensions;
using Commerce.Api.Common.Results;
using Commerce.Api.Features.Cart;
using Commerce.Api.Features.Orders.Dtos;
using Commerce.Api.Persistence;
using Commerce.Domain.Common;
using Commerce.Domain.Orders;
using Commerce.Domain.Pricing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Commerce.Api.Features.Orders;

public sealed class OrderService(
    AppDbContext db,
    CartService cartService,
    AddressService addressService,
    TimeProvider clock,
    ILogger<OrderService> logger)
{
    /// Stok çakışmasında kaç kez yeniden denensin.
    /// İki kullanıcı aynı anda son ürünü alırsa biri xmin çakışması yer;
    /// yeniden deneyince taze stoğu okur ve "tükendi" hatasını temiz verir.
    private const int MaxConcurrencyRetries = 3;

    /// Orders.IdempotencyKey kolon sınırı — veritabanı hatası yerine düzgün 400.
    private const int MaxIdempotencyKeyLength = 64;

    // ─────────────────────────────────────────────────────────
    // Sipariş oluşturma
    // ─────────────────────────────────────────────────────────
    public async Task<OrderDetailDto> CreateAsync(
        Guid userId,
        CreateOrderRequest request,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        if (idempotencyKey is { Length: > MaxIdempotencyKeyLength })
            throw new BusinessRuleException(
                $"Idempotency-Key başlığı en fazla {MaxIdempotencyKeyLength} karakter olabilir.");

        // Aynı anahtarla daha önce sipariş oluştuysa onu döndür.
        // Kullanıcı butona iki kez bastıysa ikinci istek yeni sipariş üretmez.
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await FindByKeyAsync(userId, idempotencyKey, ct);
            if (existing is not null)
            {
                logger.LogInformation(
                    "Idempotency: {Key} anahtarı için mevcut sipariş döndürüldü: {OrderNumber}",
                    idempotencyKey, existing.OrderNumber);

                return await GetByNumberAsync(userId, existing.OrderNumber, isAdmin: false, ct);
            }
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await TryCreateAsync(userId, request, idempotencyKey, ct);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                // Başka bir sipariş aynı ürünün stoğunu değiştirdi.
                // Change tracker'da bayat entity'ler var — temizle, baştan oku.
                db.ChangeTracker.Clear();

                logger.LogWarning(
                    "Stok çakışması, {Attempt}. deneme başarısız. Yeniden deneniyor. UserId: {UserId}",
                    attempt, userId);
            }
            catch (DbUpdateException ex)
                when (IsUniqueViolation(ex) && !string.IsNullOrWhiteSpace(idempotencyKey))
            {
                // Aynı Idempotency-Key ile eşzamanlı ikinci istek: filtreli unique
                // index yarışı önce yakaladı — 500 yerine kaybedenin yapması
                // gereken tek şey mevcut siparişi okumak.
                db.ChangeTracker.Clear();

                var existing = await FindByKeyAsync(userId, idempotencyKey, ct);
                if (existing is null) throw;   // çakışan şey anahtar değilse (ör. OrderNumber) yutma

                return await GetByNumberAsync(userId, existing.OrderNumber, isAdmin: false, ct);
            }
        }
    }

    private async Task<OrderDetailDto> TryCreateAsync(
        Guid userId, CreateOrderRequest request, string? idempotencyKey, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        // Transaction başlat. Kapsamı DAR tut —
        // içinde HTTP çağrısı, mail gönderimi, ödeme isteği OLMAYACAK.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Adres — sahiplik kontrolü AddressService'te (404 / 403).
        var address = await addressService.LoadOwnedAsync(userId, request.AddressId, ct);

        // Sepeti HAM oku (K1/S1). CartService.GetAsync adedi stoğa kırpar —
        // sipariş servisi kullanıcının GERÇEKTEN istediği adedi görmek zorunda.
        var owner = new CartOwner(userId, null);
        var raw = await cartService.GetRawAsync(owner, ct);

        if (raw.Items.Count == 0)
            throw new BusinessRuleException("Sepetiniz boş.");

        // Ürünleri TAKİPLİ oku. Stoğu düşeceğiz, fiyatı buradan alacağız.
        // İstemcinin gönderdiği hiçbir fiyat bilgisi kullanılmıyor.
        var productIds = raw.Items.Select(i => i.ProductId).ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var order = new Order
        {
            UserId = userId,
            Status = OrderStatus.Pending,
            CreatedAt = now,
            IdempotencyKey = idempotencyKey,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            // Adres SNAPSHOT'ı. Kullanıcı adresini silse bile eski siparişin
            // nereye gittiği kaybolmaz.
            ShippingAddress = new OrderAddress
            {
                FullName = address.FullName,
                Phone = address.Phone,
                City = address.City,
                District = address.District,
                FullAddress = address.FullAddress
            }
        };

        var lines = new List<CartLine>();

        foreach (var item in raw.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                // Ürün soft-delete edilmiş (query filter yüzünden sözlükte yok).
                // Durdurmuyoruz — satırı atlıyoruz (S3), kalanı sipariş ediliyor.
                logger.LogDebug(
                    "Sipariş: ürün {ProductId} bulunamadı, satır atlandı. UserId: {UserId}",
                    item.ProductId, userId);
                continue;
            }

            if (!product.IsActive)
            {
                logger.LogDebug(
                    "Sipariş: \"{ProductName}\" pasif, satır atlandı. UserId: {UserId}",
                    product.Name, userId);
                continue;
            }

            if (product.Stock < item.Quantity)
                throw new BusinessRuleException(
                    product.Stock == 0
                        ? $"\"{product.Name}\" tükendi."
                        : $"\"{product.Name}\" için stokta yalnızca {product.Stock} adet var.");

            // Stoktan düş. xmin concurrency token sayesinde başka biri aynı anda
            // düşürdüyse SaveChanges patlayacak (K2).
            product.Stock -= item.Quantity;

            // Fiyat SUNUCUDAN. EffectivePrice EF sorgusunda çevrilemez ama bu
            // entity bellekte materialize edilmiş — yine de refleksi bozmamak
            // için DiscountedPrice ?? Price yazılıyor.
            var unitPrice = product.DiscountedPrice ?? product.Price;
            lines.Add(new CartLine(product.Id, product.Name, product.Slug, unitPrice, item.Quantity));

            // Snapshot alanları — bu satırın anlamı ürüne bağlı KALMASIN.
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductNameSnapshot = product.Name,
                ProductSlugSnapshot = product.Slug,
                UnitPriceSnapshot = unitPrice,
                Quantity = item.Quantity
            });
        }

        if (lines.Count == 0)
            throw new BusinessRuleException("Sepetinizde sipariş verilebilir ürün kalmadı.");

        // Kuponu YENİDEN doğrula. Sepete eklendiğinden beri süresi dolmuş,
        // limiti dolmuş veya sepet tutarı minimumun altına inmiş olabilir.
        // Geçersizse siparişi DURDURMUYORUZ — sessizce indirimsiz devam (S4).
        CouponInfo? couponInfo = null;
        int? couponId = null;

        if (!string.IsNullOrWhiteSpace(raw.CouponCode))
        {
            var coupon = await db.Coupons.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Code == raw.CouponCode, ct);

            var subTotal = CartCalculator.CalculateSubTotal(lines);
            var validation = CouponValidator.Validate(coupon, subTotal, now);

            if (validation.IsValid)
            {
                couponInfo = new CouponInfo(coupon!.Code, coupon.Type, coupon.Value, coupon.MinCartTotal);
                couponId = coupon.Id;
            }
            else
            {
                logger.LogInformation(
                    "Sipariş: kupon {Code} artık geçerli değil ({Reason}), sessizce düşürüldü. UserId: {UserId}",
                    raw.CouponCode, validation.Reason, userId);
            }
        }

        // Toplam — SEPETTEKİ AYNI SINIF. Kullanıcının sepette gördüğü tutarla
        // ödeyeceği tutarın aynı olmasının garantisi bu.
        var totals = CartCalculator.Calculate(lines, couponInfo);

        // İstemci sepette gördüğü toplamı gönderdiyse ve tutmuyorsa dur (K7).
        // İstemci fiyatı KULLANILMAZ, yalnızca karşılaştırılır.
        if (request.ExpectedTotal is { } expected && expected != totals.Total)
            throw new ConflictException($"Sepet tutarı değişti. Güncel toplam: {totals.Total:N2} ₺.");

        order.SubTotal = totals.SubTotal;
        order.DiscountAmount = totals.DiscountAmount;
        order.ShippingCost = totals.ShippingCost;
        order.Total = totals.Total;
        order.CouponCode = couponInfo?.Code;
        order.OrderNumber = OrderNumberGenerator.Generate(now);

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        // Kupon sayacı — koşullu atomik UPDATE (K4). Read-modify-write iki
        // eşzamanlı siparişte limiti aşırır; WHERE'li tek UPDATE aşırmaz.
        if (couponInfo is not null && totals.DiscountAmount > 0)
        {
            var affected = await db.Coupons
                .Where(c => c.Id == couponId!.Value && (c.UsageLimit == null || c.UsedCount < c.UsageLimit))
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedCount, c => c.UsedCount + 1), ct);

            if (affected == 0)
                throw new BusinessRuleException(
                    "Kupon kullanım limiti bu sırada doldu. Sepetinizi yenileyip tekrar deneyin.");
        }

        // Sepet AYNI transaction içinde, commit'ten ÖNCE boşaltılır (K6).
        // ClearAsync kullanılıyor — elle RemoveRange, soft-delete edilmiş ürünün
        // satırını hayalet bırakırdı (Faz 6 tuzağı).
        await cartService.ClearAsync(owner, ct);

        await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Sipariş oluşturuldu: {OrderNumber}, tutar {Total}, kullanıcı {UserId}",
            order.OrderNumber, order.Total, userId);

        // Faz 9: backgroundJobs.Enqueue<IEmailService>(x => x.SendOrderConfirmationAsync(order.Id));
        // DİKKAT: commit'ten SONRA. Rollback olsaydı, kuyruğa atılmış "siparişiniz
        // alındı" maili yine de giderdi.

        return ToDetailDto(order);
    }

    // ─────────────────────────────────────────────────────────
    // Okuma
    // ─────────────────────────────────────────────────────────
    public async Task<PagedResult<OrderListDto>> GetMyOrdersAsync(
        Guid userId, PageRequest page, CancellationToken ct = default)
        => await db.Orders
            .AsNoTracking()
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .Select(o => new OrderListDto(
                o.OrderNumber,
                o.Status,
                o.Total,
                o.Items.Sum(i => i.Quantity),
                o.CreatedAt))
            .ToPagedResultAsync(page, ct);

    public async Task<OrderDetailDto> GetByNumberAsync(
        Guid userId, string orderNumber, bool isAdmin, CancellationToken ct = default)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, ct)
            ?? throw NotFoundException.For("Sipariş", orderNumber);

        EnsureOwnership(order, userId, isAdmin);
        return ToDetailDto(order);
    }

    // ─────────────────────────────────────────────────────────
    // İptal
    // ─────────────────────────────────────────────────────────
    public async Task<OrderDetailDto> CancelAsync(
        Guid userId, string orderNumber, bool isAdmin, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, ct)
            ?? throw NotFoundException.For("Sipariş", orderNumber);

        EnsureOwnership(order, userId, isAdmin);

        if (!isAdmin && !OrderStatusTransition.IsCancellableByCustomer(order.Status))
            throw new BusinessRuleException(
                $"'{order.Status}' durumundaki bir sipariş iptal edilemez. " +
                "Kargoya verilmiş siparişler için müşteri hizmetleriyle iletişime geçin.");

        // Durum makinesi son sözü söyler — geçersiz geçişte exception.
        OrderStatusTransition.EnsureCanTransition(order.Status, OrderStatus.Cancelled);

        // Stoğu GERİ EKLE. Bunu unutmak, iptal edilen her siparişte stoğu
        // sessizce eritir.
        await RestoreStockAsync(order, ct);

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = clock.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation("Sipariş iptal edildi: {OrderNumber}", order.OrderNumber);

        return ToDetailDto(order);
    }

    /// Faz 8 (Paid) ve Faz 11 (admin) bunu kullanacak — durum geçişi TEK yoldan.
    public async Task<OrderDetailDto> ChangeStatusAsync(
        string orderNumber, OrderStatus newStatus, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, ct)
            ?? throw NotFoundException.For("Sipariş", orderNumber);

        OrderStatusTransition.EnsureCanTransition(order.Status, newStatus);

        if (OrderStatusTransition.RestoresStock(newStatus))
            await RestoreStockAsync(order, ct);

        order.Status = newStatus;
        order.UpdatedAt = clock.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return ToDetailDto(order);
    }

    // ─────────────────────────────────────────────────────────
    // Yardımcılar
    // ─────────────────────────────────────────────────────────
    private async Task<Order?> FindByKeyAsync(Guid userId, string idempotencyKey, CancellationToken ct)
        => await db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.UserId == userId && o.IdempotencyKey == idempotencyKey, ct);

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private async Task RestoreStockAsync(Order order, CancellationToken ct)
    {
        var productIds = order.Items.Select(i => i.ProductId).ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        foreach (var item in order.Items)
        {
            // Ürün soft-delete edilmişse global query filter yüzünden bulunmaz.
            // Stoğu geri eklenecek bir ürün kalmamış demektir — sessizce geç.
            if (products.TryGetValue(item.ProductId, out var product))
                product.Stock += item.Quantity;
        }
    }

    /// IDOR koruması. Bu üç satırı her okuma/yazma yolunda çağırıyoruz.
    private static void EnsureOwnership(Order order, Guid userId, bool isAdmin)
    {
        if (isAdmin || order.UserId == userId) return;

        throw new ForbiddenException("Bu siparişe erişim yetkiniz yok.");
    }

    private static OrderDetailDto ToDetailDto(Order o)
        => new(
            o.OrderNumber,
            o.Status,
            o.SubTotal,
            o.ShippingCost,
            o.DiscountAmount,
            o.Total,
            o.CouponCode,
            o.Note,
            new OrderAddressDto(
                o.ShippingAddress.FullName,
                o.ShippingAddress.Phone,
                o.ShippingAddress.City,
                o.ShippingAddress.District,
                o.ShippingAddress.FullAddress),
            // Include sırası garanti değil — Id'ye göre deterministik sırala.
            o.Items
                .OrderBy(i => i.Id)
                .Select(i => new OrderItemDto(
                    i.ProductId,
                    i.ProductNameSnapshot,
                    i.ProductSlugSnapshot,
                    i.UnitPriceSnapshot,
                    i.Quantity,
                    i.LineTotal))
                .ToList(),
            OrderStatusTransition.AllowedTargets(o.Status),
            OrderStatusTransition.IsCancellableByCustomer(o.Status),
            o.CreatedAt);
}
