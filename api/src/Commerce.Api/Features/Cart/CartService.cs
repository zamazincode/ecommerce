using Commerce.Api.Common.Exceptions;
using Commerce.Api.Features.Cart.Dtos;
using Commerce.Api.Persistence;
using Commerce.Domain.Orders;
using Commerce.Domain.Pricing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Commerce.Api.Features.Cart;

public sealed class CartService(
    AppDbContext db,
    IGuestCartStore guestCarts,
    TimeProvider clock)
{
    // ─────────────────────────────────────────────────────────
    // Okuma
    // ─────────────────────────────────────────────────────────
    public async Task<CartDto> GetAsync(CartOwner owner, CancellationToken ct = default)
    {
        var (rawItems, couponCode) = owner.IsMember
            ? await ReadMemberCartAsync(owner.UserId!.Value, ct)
            : await ReadGuestCartAsync(owner.GuestId!, ct);

        return await BuildDtoAsync(rawItems, couponCode, ct);
    }

    // ─────────────────────────────────────────────────────────
    // Ekleme
    // ─────────────────────────────────────────────────────────
    public async Task<CartDto> AddItemAsync(
        CartOwner owner, AddCartItemRequest request, CancellationToken ct = default)
    {
        if (request.Quantity < 1)
            throw new BusinessRuleException("Adet en az 1 olmalı.");

        var product = await GetActiveProductAsync(request.ProductId, ct);

        if (owner.IsMember)
        {
            var cart = await GetOrCreateMemberCartAsync(owner.UserId!.Value, ct);
            var existing = cart.Items.FirstOrDefault(i => i.ProductId == product.Id);

            // Sadece YENİ satır eklenirken sınıra bak — mevcut satırın adedini
            // artırmak yeni bir "farklı ürün" değil.
            if (existing is null && cart.Items.Count >= CartLimits.MaxLinesPerCart)
                throw new BusinessRuleException(
                    $"Sepete en fazla {CartLimits.MaxLinesPerCart} farklı ürün ekleyebilirsiniz.");

            var newQuantity = (existing?.Quantity ?? 0) + request.Quantity;
            EnsureQuantityAllowed(newQuantity, product.Stock, product.Name);

            if (existing is null)
            {
                cart.Items.Add(new CartItem
                {
                    ProductId = product.Id,
                    Quantity = newQuantity,
                    UnitPriceWhenAdded = product.UnitPrice,
                    CreatedAt = clock.GetUtcNow().UtcDateTime
                });
            }
            else
            {
                // AYNI ÜRÜN İKİ SATIR OLMAZ — miktar artar.
                existing.Quantity = newQuantity;
                // Fiyat damgası tazelenir (K13): kullanıcı ürün sayfasında güncel
                // fiyatı gördü, "POST /items" bunu yeniden görmüş sayılır.
                existing.UnitPriceWhenAdded = product.UnitPrice;
            }

            cart.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            var cart = await guestCarts.GetAsync(owner.GuestId!, ct);
            var existing = cart.Items.FirstOrDefault(i => i.ProductId == product.Id);

            if (existing is null && cart.Items.Count >= CartLimits.MaxLinesPerCart)
                throw new BusinessRuleException(
                    $"Sepete en fazla {CartLimits.MaxLinesPerCart} farklı ürün ekleyebilirsiniz.");

            var newQuantity = (existing?.Quantity ?? 0) + request.Quantity;
            EnsureQuantityAllowed(newQuantity, product.Stock, product.Name);

            var index = cart.Items.FindIndex(i => i.ProductId == product.Id);
            var updated = new GuestCartItem(product.Id, newQuantity, product.UnitPrice);
            if (index < 0) cart.Items.Add(updated);
            else cart.Items[index] = updated;

            await guestCarts.SaveAsync(owner.GuestId!, cart, ct);
        }

        return await GetAsync(owner, ct);
    }

    // ─────────────────────────────────────────────────────────
    // Güncelleme / silme
    // ─────────────────────────────────────────────────────────
    public async Task<CartDto> UpdateQuantityAsync(
        CartOwner owner, int productId, int quantity, CancellationToken ct = default)
    {
        if (quantity < 1)
            throw new BusinessRuleException("Adet en az 1 olmalı. Ürünü çıkarmak için silin.");

        var product = await GetActiveProductAsync(productId, ct);

        if (owner.IsMember)
        {
            var cart = await GetOrCreateMemberCartAsync(owner.UserId!.Value, ct);
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId)
                ?? throw NotFoundException.For("Sepet öğesi", productId);

            // Önce sepette olup olmadığına bak (404), sonra stok/üst sınır kontrolü (400).
            // Sepette olmayan bir ürüne stok üstü adet gönderilince doğru cevap 404'tür.
            EnsureQuantityAllowed(quantity, product.Stock, product.Name);

            item.Quantity = quantity;
            // UnitPriceWhenAdded BİLEREK değiştirilmiyor (K13): kullanıcı sadece
            // adet değiştirdi, ürün sayfasında güncel fiyatı yeniden görmedi.
            cart.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            var cart = await guestCarts.GetAsync(owner.GuestId!, ct);
            var index = cart.Items.FindIndex(i => i.ProductId == productId);
            if (index < 0) throw NotFoundException.For("Sepet öğesi", productId);

            EnsureQuantityAllowed(quantity, product.Stock, product.Name);

            // Yerine yazılıyor (K15): RemoveAll + Add öğeyi listenin sonuna
            // atardı, adet değişince sepet arayüzünde satır zıplardı.
            cart.Items[index] = cart.Items[index] with { Quantity = quantity };
            await guestCarts.SaveAsync(owner.GuestId!, cart, ct);
        }

        return await GetAsync(owner, ct);
    }

    public async Task<CartDto> RemoveItemAsync(
        CartOwner owner, int productId, CancellationToken ct = default)
    {
        if (owner.IsMember)
        {
            var cart = await GetOrCreateMemberCartAsync(owner.UserId!.Value, ct);
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId)
                ?? throw NotFoundException.For("Sepet öğesi", productId);

            cart.Items.Remove(item);
            db.CartItems.Remove(item);
            cart.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            var cart = await guestCarts.GetAsync(owner.GuestId!, ct);
            if (cart.Items.RemoveAll(i => i.ProductId == productId) == 0)
                throw NotFoundException.For("Sepet öğesi", productId);

            await guestCarts.SaveAsync(owner.GuestId!, cart, ct);
        }

        return await GetAsync(owner, ct);
    }

    public async Task ClearAsync(CartOwner owner, CancellationToken ct = default)
    {
        if (owner.IsMember)
        {
            var userId = owner.UserId!.Value;

            // IgnoreQueryFilters ŞART: CartItem'ın "ürünü silinmemiş olsun"
            // filtresi burada da uygulanıyor ve soft-delete edilmiş ürünün
            // satırını silmeden bırakıyor. Ürün geri açılınca sepette
            // yeniden beliriyor (ölçüm 2.4) — "sepeti boşalt" sepeti boşaltmıyordu.
            await db.CartItems
                .IgnoreQueryFilters()
                .Where(i => i.Cart.UserId == userId)
                .ExecuteDeleteAsync(ct);

            await db.Carts
                .Where(c => c.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.CouponCode, (string?)null), ct);
        }
        else
        {
            await guestCarts.RemoveAsync(owner.GuestId!, ct);
        }
    }

    // ─────────────────────────────────────────────────────────
    // Kupon
    // ─────────────────────────────────────────────────────────
    public async Task<CartDto> ApplyCouponAsync(
        CartOwner owner, string code, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();

        var coupon = await db.Coupons.FirstOrDefaultAsync(c => c.Code == normalized, ct);
        var current = await GetAsync(owner, ct);

        if (current.Items.Count == 0)
            throw new BusinessRuleException("Kupon uygulamak için sepetinizde ürün olmalı.");

        var validation = CouponValidator.Validate(
            coupon, current.SubTotal, clock.GetUtcNow().UtcDateTime);

        if (!validation.IsValid)
            throw new BusinessRuleException(validation.Message!);

        if (owner.IsMember)
        {
            var cart = await GetOrCreateMemberCartAsync(owner.UserId!.Value, ct);
            cart.CouponCode = normalized;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            var cart = await guestCarts.GetAsync(owner.GuestId!, ct);
            await guestCarts.SaveAsync(owner.GuestId!, cart with { CouponCode = normalized }, ct);
        }

        return await GetAsync(owner, ct);
    }

    public async Task<CartDto> RemoveCouponAsync(CartOwner owner, CancellationToken ct = default)
    {
        if (owner.IsMember)
        {
            var cart = await GetOrCreateMemberCartAsync(owner.UserId!.Value, ct);
            cart.CouponCode = null;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            var cart = await guestCarts.GetAsync(owner.GuestId!, ct);
            await guestCarts.SaveAsync(owner.GuestId!, cart with { CouponCode = null }, ct);
        }

        return await GetAsync(owner, ct);
    }

    // ─────────────────────────────────────────────────────────
    // Birleştirme (giriş sonrası)
    // ─────────────────────────────────────────────────────────
    public async Task<CartDto> MergeAsync(
        Guid userId, string guestId, CancellationToken ct = default)
    {
        var guestCart = await guestCarts.GetAsync(guestId, ct);
        var memberCart = await GetOrCreateMemberCartAsync(userId, ct);

        if (guestCart.Items.Count > 0)
        {
            var productIds = guestCart.Items.Select(i => i.ProductId).ToList();
            var stocks = await db.Products
                .Where(p => productIds.Contains(p.Id) && p.IsActive)
                .Select(p => new { p.Id, p.Stock, p.Price, p.DiscountedPrice })
                .ToDictionaryAsync(p => p.Id, ct);

            foreach (var guestItem in guestCart.Items)
            {
                // Ürün bu arada silinmiş/pasifleşmiş olabilir — sessizce atla.
                if (!stocks.TryGetValue(guestItem.ProductId, out var product)) continue;

                var existing = memberCart.Items.FirstOrDefault(i => i.ProductId == guestItem.ProductId);

                // Üye sepeti satır sınırını aşacaksa YENİ satır sessizce atlanır —
                // kullanıcı giriş yapmaya çalışıyor, sepet yüzünden engellenmemeli.
                if (existing is null && memberCart.Items.Count >= CartLimits.MaxLinesPerCart)
                    continue;

                // KARAR: Miktarları TOPLA. Kullanıcı misafirken 2, üyeyken 1 eklediyse
                // 3 ister — büyüğünü almak (2) eklediği bir ürünü sessizce yutar.
                var merged = (existing?.Quantity ?? 0) + guestItem.Quantity;

                // Ama stok ve adet üst sınırını aşma. Sessizce kırp — hata fırlatma.
                merged = Math.Min(merged, Math.Min(product.Stock, CartLimits.MaxQuantityPerLine));
                if (merged < 1) continue;

                if (existing is null)
                {
                    memberCart.Items.Add(new CartItem
                    {
                        ProductId = guestItem.ProductId,
                        Quantity = merged,
                        UnitPriceWhenAdded = product.DiscountedPrice ?? product.Price,
                        CreatedAt = clock.GetUtcNow().UtcDateTime
                    });
                }
                else
                {
                    existing.Quantity = merged;
                }
            }
        }

        // Kupon: üyede yoksa misafirinkini devral.
        memberCart.CouponCode ??= guestCart.CouponCode;
        memberCart.UpdatedAt = clock.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(ct);
        await guestCarts.RemoveAsync(guestId, ct);

        return await GetAsync(new CartOwner(userId, null), ct);
    }

    // ─────────────────────────────────────────────────────────
    // Yardımcılar
    // ─────────────────────────────────────────────────────────
    private sealed record RawItem(int ProductId, int Quantity, decimal UnitPriceWhenAdded);

    private async Task<(List<RawItem> Items, string? CouponCode)> ReadMemberCartAsync(
        Guid userId, CancellationToken ct)
    {
        var cart = await db.Carts
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => new
            {
                c.CouponCode,
                // Deterministik sıra (K15): satırlar sepette görüldükleri sırada dursun.
                Items = c.Items.OrderBy(i => i.Id)
                               .Select(i => new RawItem(i.ProductId, i.Quantity, i.UnitPriceWhenAdded))
                               .ToList()
            })
            .FirstOrDefaultAsync(ct);

        return (cart?.Items ?? [], cart?.CouponCode);
    }

    private async Task<(List<RawItem> Items, string? CouponCode)> ReadGuestCartAsync(
        string guestId, CancellationToken ct)
    {
        var cart = await guestCarts.GetAsync(guestId, ct);
        return (cart.Items.Select(i => new RawItem(i.ProductId, i.Quantity, i.UnitPriceWhenAdded)).ToList(),
                cart.CouponCode);
    }

    /// Sepetin görünen hâlini kurar. Fiyat, ad, stok HER ZAMAN veritabanından
    /// taze okunur — depoda saklanan fiyata güvenilmez. GET her zaman GÜVENLİ:
    /// bu metot hiçbir koşulda veritabanına/Redis'e YAZMAZ.
    private async Task<CartDto> BuildDtoAsync(
        List<RawItem> rawItems, string? couponCode, CancellationToken ct)
    {
        if (rawItems.Count == 0)
            return new CartDto([], null, 0m, 0m, 0m, 0m,
                ShippingCalculator.FreeShippingThreshold, []);

        var productIds = rawItems.Select(i => i.ProductId).ToList();

        // IsActive filtrelenmiyor, SEÇİLİYOR: pasif ürünün adı uyarıda geçebilsin.
        // Soft-delete edilmiş ürün Products'ın global filtresi yüzünden zaten
        // hiç dönmez — o durumda genel bir uyarı üretiyoruz (K8).
        var products = await db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Slug,
                p.Price,
                p.DiscountedPrice,
                p.Stock,
                p.IsActive,
                ImageUrl = p.Images.OrderBy(i => i.DisplayOrder)
                                   .Select(i => i.SourceUrl).FirstOrDefault()
            })
            .ToDictionaryAsync(p => p.Id, ct);

        var items = new List<CartItemDto>();
        var warnings = new List<string>();

        foreach (var raw in rawItems)
        {
            if (!products.TryGetValue(raw.ProductId, out var product))
            {
                // Soft-delete edilmiş ürün (misafir sepeti) ya da üye tarafında
                // sorgu hiç dönmediği için buraya düşmez — burası SADECE misafir yolu.
                warnings.Add(
                    "Sepetinizdeki bir ürün artık satışta değil; sipariş adımında dikkate alınmayacak.");
                continue;
            }

            if (!product.IsActive)
            {
                warnings.Add(
                    $"\"{product.Name}\" şu anda satışta değil; sipariş adımında dikkate alınmayacak.");
                continue;
            }

            var currentPrice = product.DiscountedPrice ?? product.Price;
            var quantity = raw.Quantity;

            if (quantity > product.Stock)
            {
                quantity = product.Stock;
                warnings.Add($"\"{product.Name}\" için stok azaldı, adet {quantity} olarak gösteriliyor.");
            }

            if (quantity < 1) continue;

            var priceChanged = raw.UnitPriceWhenAdded > 0m && raw.UnitPriceWhenAdded != currentPrice;
            if (priceChanged)
                warnings.Add($"\"{product.Name}\" ürününün fiyatı güncellendi.");

            items.Add(new CartItemDto(
                product.Id, product.Name, product.Slug, product.ImageUrl,
                currentPrice, quantity,
                Math.Round(currentPrice * quantity, 2, MidpointRounding.AwayFromZero),
                product.Stock, priceChanged));
        }

        var lines = items
            .Select(i => new CartLine(i.ProductId, i.Name, i.Slug, i.UnitPrice, i.Quantity))
            .ToList();

        // Kupon doğrulaması AYNI ara toplam fonksiyonunu kullanır (K14) — aksi
        // hâlde MinCartTotal sınırında kupon "geçerli ama uygulanmadı" olabilir.
        CouponInfo? couponInfo = null;
        if (!string.IsNullOrWhiteSpace(couponCode))
        {
            var coupon = await db.Coupons.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Code == couponCode, ct);

            var subTotalForCoupon = CartCalculator.CalculateSubTotal(lines);
            var validation = CouponValidator.Validate(
                coupon, subTotalForCoupon, clock.GetUtcNow().UtcDateTime);

            if (validation.IsValid)
            {
                couponInfo = new CouponInfo(
                    coupon!.Code, coupon.Type, coupon.Value, coupon.MinCartTotal);
            }
            else
            {
                // Kupon sepete eklendikten sonra geçersizleşmiş olabilir
                // (süresi doldu, sepet tutarı azaldı). Sessizce düşür ve söyle.
                couponCode = null;
                warnings.Add($"Kupon uygulanamadı: {validation.Message}");
            }
        }

        var totals = CartCalculator.Calculate(lines, couponInfo);

        var remaining = Math.Max(
            0m, ShippingCalculator.FreeShippingThreshold - (totals.SubTotal - totals.DiscountAmount));

        return new CartDto(
            items, couponCode,
            totals.SubTotal, totals.DiscountAmount, totals.ShippingCost, totals.Total,
            Math.Round(remaining, 2), warnings.Distinct().ToList());
    }

    // Tam nitelenmiş ad ZORUNLU: bu dosyanın namespace'i (Commerce.Api.Features.Cart)
    // domain tipi Cart ile aynı adı taşıyor, derleyici namespace üyesi aramasını
    // using yönergelerinden önce yaptığı için kısa ad "Cart" burada CS0118 verir.
    private async Task<Commerce.Domain.Orders.Cart> GetOrCreateMemberCartAsync(Guid userId, CancellationToken ct)
    {
        var cart = await db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

        if (cart is not null) return cart;

        try
        {
            cart = new Commerce.Domain.Orders.Cart { UserId = userId, CreatedAt = clock.GetUtcNow().UtcDateTime };
            db.Carts.Add(cart);
            await db.SaveChangesAsync(ct);
            return cart;
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // IX_Carts_UserId UNIQUE: aynı anda gelen iki istekten biri kaybeder
            // (ölçüldü, SqlState 23505). Kaybedenin yapması gereken tek şey okumak.
            db.ChangeTracker.Clear();
            return await db.Carts.Include(c => c.Items).FirstAsync(c => c.UserId == userId, ct);
        }
    }

    private async Task<ProductSnapshot> GetActiveProductAsync(int productId, CancellationToken ct)
        => await db.Products
            .AsNoTracking()
            .Where(p => p.Id == productId && p.IsActive)
            .Select(p => new ProductSnapshot(
                p.Id, p.Name, p.Stock, p.DiscountedPrice ?? p.Price))
            .FirstOrDefaultAsync(ct)
           ?? throw NotFoundException.For("Ürün", productId);

    private static void EnsureQuantityAllowed(int quantity, int stock, string productName)
    {
        if (quantity > CartLimits.MaxQuantityPerLine)
            throw new BusinessRuleException(
                $"Bir üründen en fazla {CartLimits.MaxQuantityPerLine} adet alabilirsiniz.");

        if (quantity > stock)
            throw new BusinessRuleException(
                stock == 0
                    ? $"\"{productName}\" tükendi."
                    : $"\"{productName}\" için stokta yalnızca {stock} adet var.");
    }

    // Alan adı BİLEREK "UnitPrice" — Product.EffectivePrice EF sorgusunda
    // çevrilemez (CLAUDE.md kuralı); adı karıştırırsak refleks tetiklenir.
    private sealed record ProductSnapshot(int Id, string Name, int Stock, decimal UnitPrice);
}
