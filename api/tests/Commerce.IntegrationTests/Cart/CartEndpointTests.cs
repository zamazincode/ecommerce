using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Commerce.Api.Features.Auth;
using Commerce.Api.Features.Cart;
using Commerce.Api.Features.Cart.Dtos;
using Commerce.Api.Persistence.Identity;
using Commerce.Domain.Common;
using Commerce.Domain.Orders;
using Commerce.Domain.Pricing;
using Commerce.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Commerce.IntegrationTests.Cart;

public class CartEndpointTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly string _guestId = Guid.NewGuid().ToString();

    private HttpRequestMessage GuestRequest(HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add(CartOwner.GuestHeader, _guestId);
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    private async Task<int> SeedProductAsync(
        string name = "Test Kitabı", decimal price = 100m, int stock = 20, bool isActive = true)
    {
        var id = 0;
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            var builder = new ProductBuilder()
                .WithName(name).WithPrice(price).WithStock(stock).InCategory(category);
            if (!isActive) builder = builder.Inactive();
            var product = builder.Build();
            db.Products.Add(product);
            await db.SaveChangesAsync();
            id = product.Id;
        });
        return id;
    }

    private async Task SeedCouponAsync(
        string code, CouponType type, decimal value,
        decimal minCartTotal, int validFromDays, int validToDays)
    {
        await ExecuteDbAsync(async db =>
        {
            db.Coupons.Add(new Coupon
            {
                Code = code,
                Type = type,
                Value = value,
                MinCartTotal = minCartTotal,
                ValidFrom = DateTime.UtcNow.AddDays(validFromDays),
                ValidTo = DateTime.UtcNow.AddDays(validToDays),
                IsActive = true
            });
            await db.SaveChangesAsync();
        });
    }

    // ═══════════════════════════════════════════════════════════
    // A. Kimlik ve sahiplik çözümleme
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task Cart_WithoutAnyIdentity_Returns400()
    {
        var response = await Client.GetAsync("/api/cart", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12345")]
    [InlineData("../../etc")]
    public async Task Cart_WithMalformedGuestIdHeader_Returns400(string malformedGuestId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/cart");
        request.Headers.Add(CartOwner.GuestHeader, malformedGuestId);

        var response = await Client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cart_WithExpiredAccessToken_Returns401_NotEmptyGuestCart()
    {
        var userId = await CreateUserAsync("sureli-sepet@test.com", "Test1234", AppRoles.Customer);

        // Thread.Sleep YAZMIYORUZ. TokenService'i doğrudan çözüp geçmişte biten
        // bir token üretiyoruz (kalıp: AuthEndpointTests.Me_WithExpiredToken_Returns401).
        string expiredToken;
        using (var scope = Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var tokens = scope.ServiceProvider.GetRequiredService<TokenService>();

            var user = await users.FindByIdAsync(userId.ToString());
            (expiredToken, _) = tokens.CreateAccessToken(user!, [AppRoles.Customer],
                lifetime: TimeSpan.FromMinutes(-5));
        }

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        // T2 / Ölçüm 2.5: bu istek 200 + boş misafir sepeti DÖNMEMELİ.
        var response = await Client.GetAsync("/api/cart", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MemberIdentity_WinsOverGuestHeader_WhenBothPresent()
    {
        var productId = await SeedProductAsync();
        await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
            new { productId, quantity = 1 }), Ct);

        await AuthenticateAsync();

        var combined = new HttpRequestMessage(HttpMethod.Get, "/api/cart");
        combined.Headers.Add(CartOwner.GuestHeader, _guestId);
        var response = await Client.SendAsync(combined, Ct);
        var cart = await response.Content.ReadFromJsonAsync<CartDto>(Ct);

        // Üye kimliği kazanır: dönen sepet ÜYENİN (boş) sepeti, misafirin değil.
        cart!.Items.ShouldBeEmpty();

        // Misafir sepeti bu istekten etkilenmemiş olmalı.
        ClearAuthentication();
        var guestCart = await (await Client.SendAsync(GuestRequest(HttpMethod.Get, "/api/cart"), Ct))
            .Content.ReadFromJsonAsync<CartDto>(Ct);
        guestCart!.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Merge_WithoutToken_Returns401()
    {
        var response = await Client.PostAsync("/api/cart/merge", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ═══════════════════════════════════════════════════════════
    // B. Misafir sepeti
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GuestCart_AddAndRead_Works()
    {
        var productId = await SeedProductAsync(price: 150m);

        var add = await Client.SendAsync(
            GuestRequest(HttpMethod.Post, "/api/cart/items", new { productId, quantity = 2 }), Ct);
        add.StatusCode.ShouldBe(HttpStatusCode.OK);

        var cart = await (await Client.SendAsync(GuestRequest(HttpMethod.Get, "/api/cart"), Ct))
            .Content.ReadFromJsonAsync<CartDto>(Ct);

        cart!.Items.Count.ShouldBe(1);
        cart.Items[0].Quantity.ShouldBe(2);
        cart.SubTotal.ShouldBe(300m);
        cart.ShippingCost.ShouldBe(0m);      // 300 ≥ 200
        cart.Total.ShouldBe(300m);
    }

    [Fact]
    public async Task AddingSameProductTwice_IncrementsQuantityWithoutSecondLine()
    {
        var productId = await SeedProductAsync();

        await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
            new { productId, quantity = 2 }), Ct);
        var second = await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
            new { productId, quantity = 3 }), Ct);

        var cart = await second.Content.ReadFromJsonAsync<CartDto>(Ct);

        cart!.Items.Count.ShouldBe(1);
        cart.Items[0].Quantity.ShouldBe(5);
    }

    [Fact]
    public async Task AddItem_ExceedingStock_Returns400()
    {
        var productId = await SeedProductAsync(stock: 2);

        var response = await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
            new { productId, quantity = 5 }), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("2");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(11)]     // CartLimits.MaxQuantityPerLine = 10
    [InlineData(999999)]
    public async Task AddItem_WithInvalidQuantity_Returns400(int quantity)
    {
        var productId = await SeedProductAsync(stock: 1000);

        var response = await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
            new { productId, quantity }), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddItem_WithNonExistentProduct_Returns404()
    {
        var response = await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
            new { productId = 999_999, quantity = 1 }), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveItem_RemovesLineFromCart()
    {
        var productId = await SeedProductAsync();
        await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
            new { productId, quantity = 1 }), Ct);

        var response = await Client.SendAsync(
            GuestRequest(HttpMethod.Delete, $"/api/cart/items/{productId}"), Ct);

        var cart = await response.Content.ReadFromJsonAsync<CartDto>(Ct);
        cart!.Items.ShouldBeEmpty();
        cart.Total.ShouldBe(0m);
    }

    // ═══════════════════════════════════════════════════════════
    // C. Adet güncelleme
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateQuantity_ChangesLineQuantity()
    {
        var productId = await SeedProductAsync(price: 50m, stock: 20);
        await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
            new { productId, quantity = 1 }), Ct);

        var response = await Client.SendAsync(GuestRequest(HttpMethod.Patch,
            $"/api/cart/items/{productId}", new { quantity = 4 }), Ct);

        var cart = await response.Content.ReadFromJsonAsync<CartDto>(Ct);
        cart!.Items[0].Quantity.ShouldBe(4);
        cart.Items[0].LineTotal.ShouldBe(200m);
    }

    [Fact]
    public async Task UpdateQuantity_ForItemNotInCart_Returns404()
    {
        var productId = await SeedProductAsync();

        // Sepete HİÇ eklenmedi — direkt PATCH.
        var response = await Client.SendAsync(GuestRequest(HttpMethod.Patch,
            $"/api/cart/items/{productId}", new { quantity = 2 }), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateQuantity_ExceedingStock_Returns400()
    {
        var productId = await SeedProductAsync(stock: 3);
        await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
            new { productId, quantity = 1 }), Ct);

        var response = await Client.SendAsync(GuestRequest(HttpMethod.Patch,
            $"/api/cart/items/{productId}", new { quantity = 8 }), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ═══════════════════════════════════════════════════════════
    // D. Üye sepeti
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task MemberCart_PersistsInDatabase()
    {
        var userId = await AuthenticateAsync();
        var productId = await SeedProductAsync();

        await Client.PostAsJsonAsync("/api/cart/items", new { productId, quantity = 3 }, Ct);

        var stored = await ExecuteDbAsync(db => db.CartItems
            .Include(i => i.Cart)
            .FirstOrDefaultAsync(i => i.Cart.UserId == userId));

        stored.ShouldNotBeNull();
        stored.ProductId.ShouldBe(productId);
        stored.Quantity.ShouldBe(3);
        stored.UnitPriceWhenAdded.ShouldBeGreaterThan(0m);
    }

    [Fact]
    public async Task MemberCart_IsIsolatedBetweenUsers()
    {
        // Kullanıcı A sepete ekliyor
        await AuthenticateAsync("a-sepet@test.com");
        var productId = await SeedProductAsync();
        await Client.PostAsJsonAsync("/api/cart/items", new { productId, quantity = 2 }, Ct);

        // Kullanıcı B'nin sepeti boş olmalı — ve A'nın öğesini silememeli
        ClearAuthentication();
        await AuthenticateAsync("b-sepet@test.com");

        var cartB = await Client.GetFromJsonAsync<CartDto>("/api/cart", Ct);
        cartB!.Items.ShouldBeEmpty();

        var deleteAttempt = await Client.DeleteAsync($"/api/cart/items/{productId}", Ct);
        deleteAttempt.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // A'nın sepeti bozulmamış olmalı
        var stillThere = await ExecuteDbAsync(db => db.CartItems.CountAsync());
        stillThere.ShouldBe(1);
    }

    [Fact]
    public async Task ClearCart_EmptiesEverything()
    {
        var productId = await SeedProductAsync();
        await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
            new { productId, quantity = 2 }), Ct);

        var clear = await Client.SendAsync(GuestRequest(HttpMethod.Delete, "/api/cart"), Ct);
        clear.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var cart = await (await Client.SendAsync(GuestRequest(HttpMethod.Get, "/api/cart"), Ct))
            .Content.ReadFromJsonAsync<CartDto>(Ct);
        cart!.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task ClearCart_AlsoDeletesRowsOfSoftDeletedProducts()
    {
        await AuthenticateAsync();
        var productA = await SeedProductAsync(name: "Kalıcı Ürün");
        var productB = await SeedProductAsync(name: "Silinecek Ürün");

        await Client.PostAsJsonAsync("/api/cart/items", new { productId = productA, quantity = 1 }, Ct);
        await Client.PostAsJsonAsync("/api/cart/items", new { productId = productB, quantity = 1 }, Ct);

        // productB soft-delete edilir — ürün "geri açılınca sepette yeniden
        // beliriyor" hatasını (T1/Ölçüm 2.4) burada test ediyoruz.
        await ExecuteDbAsync(async db =>
            await db.Products.Where(p => p.Id == productB)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.DeletedAt, DateTime.UtcNow), Ct));

        var clear = await Client.DeleteAsync("/api/cart", Ct);
        clear.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // IgnoreQueryFilters OLMADAN bu sayı 1 çıkar (silinen ürünün "hayalet" satırı).
        var remaining = await ExecuteDbAsync(db => db.CartItems.IgnoreQueryFilters().CountAsync());
        remaining.ShouldBe(0);
    }

    // ═══════════════════════════════════════════════════════════
    // E. Birleştirme
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task Merge_SumsQuantitiesFromGuestAndMemberCarts()
    {
        var productId = await SeedProductAsync(stock: 20);

        // Misafirken 2 adet
        await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
            new { productId, quantity = 2 }), Ct);

        // Giriş yap, üye sepetine 1 adet ekle
        await AuthenticateAsync();
        await Client.PostAsJsonAsync("/api/cart/items", new { productId, quantity = 1 }, Ct);

        // Birleştir
        var merge = new HttpRequestMessage(HttpMethod.Post, "/api/cart/merge");
        merge.Headers.Add(CartOwner.GuestHeader, _guestId);
        var response = await Client.SendAsync(merge, Ct);

        var cart = await response.Content.ReadFromJsonAsync<CartDto>(Ct);
        cart!.Items.Count.ShouldBe(1);
        cart.Items[0].Quantity.ShouldBe(3);   // 2 + 1
    }

    [Fact]
    public async Task Merge_ClampsToAvailableStock()
    {
        var productId = await SeedProductAsync(stock: 4);

        await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
            new { productId, quantity = 3 }), Ct);

        await AuthenticateAsync();
        await Client.PostAsJsonAsync("/api/cart/items", new { productId, quantity = 3 }, Ct);

        var merge = new HttpRequestMessage(HttpMethod.Post, "/api/cart/merge");
        merge.Headers.Add(CartOwner.GuestHeader, _guestId);
        var cart = await (await Client.SendAsync(merge, Ct)).Content.ReadFromJsonAsync<CartDto>(Ct);

        // 3 + 3 = 6 ama stok 4 → 4'e kırpılmalı, hata FIRLATILMAMALI
        cart!.Items[0].Quantity.ShouldBe(4);
    }

    [Fact]
    public async Task Merge_ClearsGuestCartAfterwards()
    {
        var productId = await SeedProductAsync();
        await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
            new { productId, quantity = 1 }), Ct);

        await AuthenticateAsync();
        var merge = new HttpRequestMessage(HttpMethod.Post, "/api/cart/merge");
        merge.Headers.Add(CartOwner.GuestHeader, _guestId);
        await Client.SendAsync(merge, Ct);

        // Misafir sepeti temizlenmiş olmalı
        ClearAuthentication();
        var guestCart = await (await Client.SendAsync(GuestRequest(HttpMethod.Get, "/api/cart"), Ct))
            .Content.ReadFromJsonAsync<CartDto>(Ct);

        guestCart!.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Merge_WithoutGuestIdHeader_Returns400()
    {
        await AuthenticateAsync();

        var response = await Client.PostAsync("/api/cart/merge", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ═══════════════════════════════════════════════════════════
    // F. Kupon
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task ApplyCoupon_WithValidCode_NormalizesCodeAndReducesTotal()
    {
        var productId = await SeedProductAsync(price: 200m);
        await SeedCouponAsync("ON10", CouponType.Percentage, 10m, minCartTotal: 100m,
            validFromDays: -1, validToDays: 30);

        await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
            new { productId, quantity = 1 }), Ct);

        var response = await Client.SendAsync(
            GuestRequest(HttpMethod.Post, "/api/cart/coupon", new { code = "on10" }), Ct);

        var cart = await response.Content.ReadFromJsonAsync<CartDto>(Ct);
        cart!.CouponCode.ShouldBe("ON10");     // büyük harfe normalize edildi
        cart.DiscountAmount.ShouldBe(20m);
        cart.Total.ShouldBe(209.90m);          // 200 - 20 + 29.90 (180 < 200 eşiği)
    }

    [Fact]
    public async Task ApplyCoupon_WhenExpired_Returns400()
    {
        var productId = await SeedProductAsync(price: 500m);
        await SeedCouponAsync("GECMIS", CouponType.Percentage, 20m, minCartTotal: 0m,
            validFromDays: -730, validToDays: -1);

        await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
            new { productId, quantity = 1 }), Ct);

        var response = await Client.SendAsync(
            GuestRequest(HttpMethod.Post, "/api/cart/coupon", new { code = "GECMIS" }), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("süresi");
    }

    [Fact]
    public async Task ApplyCoupon_WithUnknownCode_Returns400()
    {
        var productId = await SeedProductAsync();
        await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
            new { productId, quantity = 1 }), Ct);

        var response = await Client.SendAsync(
            GuestRequest(HttpMethod.Post, "/api/cart/coupon", new { code = "BOYLEBIRKUPONYOK" }), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ApplyCoupon_WhenMinCartTotalNotMet_Returns400()
    {
        var productId = await SeedProductAsync(price: 50m);
        await SeedCouponAsync("MIN300", CouponType.Percentage, 10m, minCartTotal: 300m,
            validFromDays: -1, validToDays: 30);

        await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
            new { productId, quantity = 1 }), Ct);

        var response = await Client.SendAsync(
            GuestRequest(HttpMethod.Post, "/api/cart/coupon", new { code = "MIN300" }), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ApplyCoupon_OnEmptyCart_Returns400()
    {
        var response = await Client.SendAsync(
            GuestRequest(HttpMethod.Post, "/api/cart/coupon", new { code = "HERHANGI" }), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RemoveCoupon_ClearsDiscount()
    {
        var productId = await SeedProductAsync(price: 200m);
        await SeedCouponAsync("ON10", CouponType.Percentage, 10m, minCartTotal: 100m,
            validFromDays: -1, validToDays: 30);

        await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
            new { productId, quantity = 1 }), Ct);
        await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/coupon", new { code = "ON10" }), Ct);

        var response = await Client.SendAsync(GuestRequest(HttpMethod.Delete, "/api/cart/coupon"), Ct);

        var cart = await response.Content.ReadFromJsonAsync<CartDto>(Ct);
        cart!.CouponCode.ShouldBeNull();
        cart.DiscountAmount.ShouldBe(0m);
    }

    // ═══════════════════════════════════════════════════════════
    // G. Fiyat / stok / kullanılabilirlik
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetCart_WhenPriceChangedAfterAdding_ReturnsWarningAndCurrentPrice()
    {
        var productId = await SeedProductAsync(price: 100m);
        await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
            new { productId, quantity = 1 }), Ct);

        // Fiyat değişti
        await ExecuteDbAsync(async db =>
            await db.Products.Where(p => p.Id == productId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Price, 180m), Ct));

        var cart = await (await Client.SendAsync(GuestRequest(HttpMethod.Get, "/api/cart"), Ct))
            .Content.ReadFromJsonAsync<CartDto>(Ct);

        // GÜNCEL fiyat gösterilir — sepette fiyat dondurulmaz
        cart!.Items[0].UnitPrice.ShouldBe(180m);
        cart.Items[0].PriceChanged.ShouldBeTrue();
        cart.Warnings.ShouldContain(w => w.Contains("fiyatı güncellendi"));
    }

    [Fact]
    public async Task GetCart_WhenStockDroppedBelowQuantity_ClampsAndWarns()
    {
        var productId = await SeedProductAsync(stock: 10);
        await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
            new { productId, quantity = 6 }), Ct);

        await ExecuteDbAsync(async db =>
            await db.Products.Where(p => p.Id == productId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, 2), Ct));

        var cart = await (await Client.SendAsync(GuestRequest(HttpMethod.Get, "/api/cart"), Ct))
            .Content.ReadFromJsonAsync<CartDto>(Ct);

        cart!.Items[0].Quantity.ShouldBe(2);
        cart.Warnings.ShouldContain(w => w.Contains("stok azaldı"));
    }

    [Fact]
    public async Task GetCart_WhenProductDeactivated_ExcludesLineAndWarns()
    {
        var productId = await SeedProductAsync(name: "Pasifleşen Kitap");
        await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
            new { productId, quantity = 1 }), Ct);

        await ExecuteDbAsync(async db =>
            await db.Products.Where(p => p.Id == productId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false), Ct));

        var cart = await (await Client.SendAsync(GuestRequest(HttpMethod.Get, "/api/cart"), Ct))
            .Content.ReadFromJsonAsync<CartDto>(Ct);

        cart!.Items.ShouldBeEmpty();
        cart.Warnings.ShouldContain(w => w.Contains("Pasifleşen Kitap"));
        // K8/S7: GET hiçbir şey YAZMAZ, kılavuzun "çıkarıldı" ifadesi YALAN —
        // mesaj gerçeği söylüyor olmalı.
        cart.Warnings.ShouldAllBe(w => !w.Contains("çıkarıldı"));
    }

    [Fact]
    public async Task Coupon_ThatBecameInvalidLater_IsDroppedSilentlyWithWarning()
    {
        var expensive = await SeedProductAsync(name: "Pahalı Kitap", price: 350m);
        var cheap = await SeedProductAsync(name: "Ucuz Kitap", price: 50m);
        await SeedCouponAsync("MIN300", CouponType.Percentage, 10m, minCartTotal: 300m,
            validFromDays: -1, validToDays: 30);

        await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
            new { productId = expensive, quantity = 1 }), Ct);
        await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
            new { productId = cheap, quantity = 1 }), Ct);

        var applied = await (await Client.SendAsync(
            GuestRequest(HttpMethod.Post, "/api/cart/coupon", new { code = "MIN300" }), Ct))
            .Content.ReadFromJsonAsync<CartDto>(Ct);
        applied!.CouponCode.ShouldBe("MIN300");

        // Pahalı satırı sil — geriye kalan 50₺'lik sepet artık min 300'ü sağlamıyor.
        await Client.SendAsync(GuestRequest(HttpMethod.Delete, $"/api/cart/items/{expensive}"), Ct);

        var cart = await (await Client.SendAsync(GuestRequest(HttpMethod.Get, "/api/cart"), Ct))
            .Content.ReadFromJsonAsync<CartDto>(Ct);

        cart!.CouponCode.ShouldBeNull();
        cart.DiscountAmount.ShouldBe(0m);
        cart.Warnings.ShouldContain(w => w.Contains("Kupon uygulanamadı"));
    }

    // ═══════════════════════════════════════════════════════════
    // H. Satır sınırı
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AddItem_WhenDistinctLineLimitExceeded_Returns400()
    {
        List<int> productIds = [];
        await ExecuteDbAsync(async db =>
        {
            var category = await CatalogTestData.SeedCategoryWithProductsAsync(
                db, CartLimits.MaxLinesPerCart + 1);
            productIds = await db.Products.Where(p => p.CategoryId == category.Id)
                .OrderBy(p => p.Id).Select(p => p.Id).ToListAsync();
        });

        for (var i = 0; i < CartLimits.MaxLinesPerCart; i++)
        {
            var response = await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
                new { productId = productIds[i], quantity = 1 }), Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var last = await Client.SendAsync(GuestRequest(HttpMethod.Post, "/api/cart/items",
            new { productId = productIds[CartLimits.MaxLinesPerCart], quantity = 1 }), Ct);

        last.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
