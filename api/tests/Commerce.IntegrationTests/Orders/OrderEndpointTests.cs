using System.Net;
using System.Net.Http.Json;
using Commerce.Api.Common.Results;
using Commerce.Api.Features.Cart.Dtos;
using Commerce.Api.Features.Orders;
using Commerce.Api.Features.Orders.Dtos;
using Commerce.Api.Persistence.Identity;
using Commerce.Domain.Common;
using Commerce.Domain.Orders;
using Commerce.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Commerce.IntegrationTests.Orders;

public class OrderEndpointTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<int> SeedProductAsync(
        string name = "Test Kitabı", decimal price = 150m, decimal? discountedPrice = null,
        int stock = 20, bool isActive = true)
    {
        var id = 0;
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            var builder = new ProductBuilder()
                .WithName(name).WithPrice(price).WithStock(stock).InCategory(category);
            if (discountedPrice is not null) builder = builder.WithDiscount(discountedPrice);
            if (!isActive) builder = builder.Inactive();
            var product = builder.Build();
            db.Products.Add(product);
            await db.SaveChangesAsync();
            id = product.Id;
        });
        return id;
    }

    private async Task<int> CreateAddressAsync()
    {
        var response = await Client.PostAsJsonAsync("/api/addresses", new
        {
            title = "Ev",
            fullName = "Ali Veli",
            phone = "5551112233",
            city = "İstanbul",
            district = "Kadıköy",
            fullAddress = "Moda Caddesi No 1 Daire 5",
            isDefault = true
        }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var address = await response.Content.ReadFromJsonAsync<AddressDto>(Ct);
        return address!.Id;
    }

    private async Task AddToCartAsync(int productId, int quantity)
    {
        var response = await Client.PostAsJsonAsync("/api/cart/items", new { productId, quantity }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task SeedCouponAsync(
        string code, CouponType type, decimal value,
        decimal minCartTotal, int validFromDays, int validToDays, int? usageLimit = null)
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
                UsageLimit = usageLimit,
                IsActive = true
            });
            await db.SaveChangesAsync();
        });
    }

    private async Task<HttpResponseMessage> PostOrderAsync(
        int addressId, string? key = null, decimal? expectedTotal = null, string? note = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new { addressId, note, expectedTotal })
        };
        if (!string.IsNullOrWhiteSpace(key))
            request.Headers.Add(OrderEndpoints.IdempotencyHeader, key);

        return await Client.SendAsync(request, Ct);
    }

    // ═══════════════════════════════════════════════════════════
    // A. Oluşturma
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateOrder_WithValidCart_Returns201AndPersistsOrder()
    {
        await AuthenticateAsync();
        var productId = await SeedProductAsync(price: 150m, stock: 20);
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 2);

        var response = await PostOrderAsync(addressId);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var order = await response.Content.ReadFromJsonAsync<OrderDetailDto>(Ct);
        order!.OrderNumber.ShouldStartWith("ORD-");
        order.Status.ShouldBe(OrderStatus.Pending);
        order.SubTotal.ShouldBe(300m);
        order.ShippingCost.ShouldBe(0m);      // 300 ≥ 200 → bedava
        order.Total.ShouldBe(300m);
        order.Items.Count.ShouldBe(1);

        var persisted = await ExecuteDbAsync(db =>
            db.Orders.Include(o => o.Items).FirstOrDefaultAsync());
        persisted.ShouldNotBeNull();
        persisted.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CreateOrder_WithEmptyCart_Returns400()
    {
        await AuthenticateAsync();
        var addressId = await CreateAddressAsync();

        var response = await PostOrderAsync(addressId);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("boş");
    }

    [Fact]
    public async Task CreateOrder_DecrementsStock()
    {
        await AuthenticateAsync();
        var productId = await SeedProductAsync(stock: 20);
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 3);

        await PostOrderAsync(addressId);

        var stock = await ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == productId).Select(p => p.Stock).FirstAsync());

        stock.ShouldBe(17);
    }

    [Fact]
    public async Task CreateOrder_EmptiesCart()
    {
        await AuthenticateAsync();
        var productId = await SeedProductAsync();
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 2);

        await PostOrderAsync(addressId);

        var cart = await Client.GetFromJsonAsync<CartDto>("/api/cart", Ct);
        cart!.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateOrder_WhenStockDroppedBelowCartQuantity_Returns400AndRollsBack()
    {
        await AuthenticateAsync();
        var productId = await SeedProductAsync(stock: 2);
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 2);

        // Sepete eklendikten SONRA stok düşsün. GetRawAsync sepeti KIRPMADAN
        // okur — istenen adet (2) hâlâ 2'dir. GetAsync burada 1'e kırpardı ve
        // bu test yanlış sebeple geçerdi (plan ölçüm 2.3'ün regresyon testi).
        await ExecuteDbAsync(async db =>
            await db.Products.Where(p => p.Id == productId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, 1)));

        var response = await PostOrderAsync(addressId);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Transaction rollback oldu — stok DEĞİŞMEMİŞ olmalı.
        var stock = await ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == productId).Select(p => p.Stock).FirstAsync());
        stock.ShouldBe(1);

        var orderCount = await ExecuteDbAsync(db => db.Orders.CountAsync());
        orderCount.ShouldBe(0);
    }

    [Fact]
    public async Task CreateOrder_SnapshotsProductNameAndPrice()
    {
        // BU FAZIN EN ÖNEMLİ TESTİ.
        // Snapshot alınmasaydı geçmiş siparişin tutarı bugün değişirdi.
        await AuthenticateAsync();
        var productId = await SeedProductAsync(name: "Suç ve Ceza", price: 180m, stock: 10);
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 1);

        var created = await (await PostOrderAsync(addressId)).Content.ReadFromJsonAsync<OrderDetailDto>(Ct);

        // Sipariş verildikten SONRA ürünü değiştir.
        await ExecuteDbAsync(async db =>
            await db.Products.Where(p => p.Id == productId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Price, 260m)
                    .SetProperty(p => p.Name, "Suç ve Ceza (Yeni Baskı)")));

        var fetched = await Client.GetFromJsonAsync<OrderDetailDto>(
            $"/api/orders/{created!.OrderNumber}", Ct);

        fetched!.Items[0].ProductName.ShouldBe("Suç ve Ceza");   // ESKİ ad
        fetched.Items[0].UnitPrice.ShouldBe(180m);               // ESKİ fiyat
        fetched.Total.ShouldBe(created.Total);                   // toplam değişmedi
    }

    [Fact]
    public async Task CreateOrder_UsesDiscountedPriceAsUnitPrice()
    {
        await AuthenticateAsync();
        var productId = await SeedProductAsync(price: 300m, discountedPrice: 209.70m, stock: 10);
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 1);

        var order = await (await PostOrderAsync(addressId)).Content.ReadFromJsonAsync<OrderDetailDto>(Ct);

        order!.Items[0].UnitPrice.ShouldBe(209.70m);
    }

    [Fact]
    public async Task CreateOrder_CopiesAddressAsSnapshot()
    {
        await AuthenticateAsync();
        var productId = await SeedProductAsync();
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 1);

        var created = await (await PostOrderAsync(addressId)).Content.ReadFromJsonAsync<OrderDetailDto>(Ct);

        // Adresi sil — sipariş etkilenmemeli.
        (await Client.DeleteAsync($"/api/addresses/{addressId}", Ct)).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        var fetched = await Client.GetFromJsonAsync<OrderDetailDto>(
            $"/api/orders/{created!.OrderNumber}", Ct);

        fetched!.ShippingAddress.City.ShouldBe("İstanbul");
        fetched.ShippingAddress.FullAddress.ShouldContain("Moda");
    }

    [Fact]
    public async Task CreateOrder_SkipsInactiveProduct_AndOrdersRemainingLines()
    {
        await AuthenticateAsync();
        var activeId = await SeedProductAsync(name: "Aktif Kitap", price: 100m, stock: 10);
        var inactiveId = await SeedProductAsync(name: "Pasifleşecek Kitap", price: 50m, stock: 10);
        var addressId = await CreateAddressAsync();

        await AddToCartAsync(activeId, 1);
        await AddToCartAsync(inactiveId, 1);

        // Sepete eklendikten SONRA pasifleştir — CartService.GetRawAsync bu
        // durumu bilerek görmezden gelir, sipariş servisi satırı ATLAR (S3).
        await ExecuteDbAsync(async db =>
            await db.Products.Where(p => p.Id == inactiveId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false)));

        var response = await PostOrderAsync(addressId);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var order = await response.Content.ReadFromJsonAsync<OrderDetailDto>(Ct);
        order!.Items.Count.ShouldBe(1);
        order.Items[0].ProductId.ShouldBe(activeId);
        order.SubTotal.ShouldBe(100m);

        var inactiveStock = await ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == inactiveId).Select(p => p.Stock).FirstAsync());
        inactiveStock.ShouldBe(10);
    }

    [Fact]
    public async Task CreateOrder_WhenNoOrderableLineRemains_Returns400()
    {
        await AuthenticateAsync();
        var productId = await SeedProductAsync();
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 1);

        await ExecuteDbAsync(async db =>
            await db.Products.Where(p => p.Id == productId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false)));

        var response = await PostOrderAsync(addressId);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var orderCount = await ExecuteDbAsync(db => db.Orders.CountAsync());
        orderCount.ShouldBe(0);
    }

    [Fact]
    public async Task CreateOrder_WithMismatchedExpectedTotal_Returns409_AndCreatesNothing()
    {
        await AuthenticateAsync();
        var productId = await SeedProductAsync(price: 150m, stock: 20);
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 2);

        var response = await PostOrderAsync(addressId, expectedTotal: 1m);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var orderCount = await ExecuteDbAsync(db => db.Orders.CountAsync());
        orderCount.ShouldBe(0);

        var stock = await ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == productId).Select(p => p.Stock).FirstAsync());
        stock.ShouldBe(20);
    }

    // ═══════════════════════════════════════════════════════════
    // B. Yetkilendirme (IDOR)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateOrder_WithAnotherUsersAddress_Returns403()
    {
        // Kullanıcı A adres oluşturur.
        await AuthenticateAsync("adres-sahibi@test.com");
        var addressId = await CreateAddressAsync();

        // Kullanıcı B o adresle sipariş vermeye çalışır.
        ClearAuthentication();
        await AuthenticateAsync("baska-siparis@test.com");
        var productId = await SeedProductAsync();
        await AddToCartAsync(productId, 1);

        var response = await PostOrderAsync(addressId);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateOrder_WithoutToken_Returns401()
    {
        var response = await PostOrderAsync(addressId: 1);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOrder_WhenRequestedByDifferentUser_Returns403()
    {
        // BU TESTİ MUTLAKA YAZ. Gerçek sitelerde bulunmuş bir açıktır.
        await AuthenticateAsync("siparis-sahibi@test.com");
        var productId = await SeedProductAsync();
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 1);

        var created = await (await PostOrderAsync(addressId)).Content.ReadFromJsonAsync<OrderDetailDto>(Ct);

        ClearAuthentication();
        await AuthenticateAsync("saldirgan@test.com");

        var response = await Client.GetAsync($"/api/orders/{created!.OrderNumber}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CancelOrder_OfAnotherUser_Returns403()
    {
        await AuthenticateAsync("iptal-sahibi@test.com");
        var productId = await SeedProductAsync();
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 1);

        var created = await (await PostOrderAsync(addressId)).Content.ReadFromJsonAsync<OrderDetailDto>(Ct);

        ClearAuthentication();
        await AuthenticateAsync("iptal-saldirgan@test.com");

        var response = await Client.PostAsync($"/api/orders/{created!.OrderNumber}/cancel", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ═══════════════════════════════════════════════════════════
    // C. Idempotency
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateOrder_WithSameIdempotencyKeyTwice_CreatesOnlyOneOrderAndOneStockDrop()
    {
        await AuthenticateAsync();
        var productId = await SeedProductAsync(stock: 20);
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 2);

        var key = Guid.NewGuid().ToString();

        var first = await PostOrderAsync(addressId, key);
        var second = await PostOrderAsync(addressId, key);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Created);

        var order1 = await first.Content.ReadFromJsonAsync<OrderDetailDto>(Ct);
        var order2 = await second.Content.ReadFromJsonAsync<OrderDetailDto>(Ct);

        order2!.OrderNumber.ShouldBe(order1!.OrderNumber);

        var orderCount = await ExecuteDbAsync(db => db.Orders.CountAsync());
        orderCount.ShouldBe(1);

        // Stok da yalnızca BİR KEZ düşmüş olmalı.
        var stock = await ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == productId).Select(p => p.Stock).FirstAsync());
        stock.ShouldBe(18);
    }

    // ═══════════════════════════════════════════════════════════
    // D. Okuma
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetMyOrders_WithoutQueryString_Returns200()
    {
        // Ölçüm 2.5 regresyonu: [AsParameters] PageRequest doğrudan bağlansaydı
        // query string'siz istek 400 dönerdi.
        await AuthenticateAsync();

        var response = await Client.GetAsync("/api/orders", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMyOrders_ReturnsOnlyOwnOrders()
    {
        await AuthenticateAsync("birinci@test.com");
        var productId = await SeedProductAsync(stock: 50);
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 1);
        await PostOrderAsync(addressId);

        ClearAuthentication();
        await AuthenticateAsync("ikinci@test.com");

        var result = await Client.GetFromJsonAsync<PagedResult<OrderListDto>>("/api/orders", Ct);

        result!.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetOrder_AsAdmin_CanViewAnyOrder()
    {
        await AuthenticateAsync("musteri@test.com");
        var productId = await SeedProductAsync();
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 1);

        var created = await (await PostOrderAsync(addressId)).Content.ReadFromJsonAsync<OrderDetailDto>(Ct);

        ClearAuthentication();
        await AuthenticateAsync("yonetici@test.com", role: AppRoles.Admin);

        var response = await Client.GetAsync($"/api/orders/{created!.OrderNumber}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetOrder_WithUnknownOrderNumber_Returns404()
    {
        await AuthenticateAsync();

        var response = await Client.GetAsync("/api/orders/ORD-20260101-ZZZZZZ", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ═══════════════════════════════════════════════════════════
    // E. İptal ve durum
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task CancelOrder_WhenPending_RestoresStock()
    {
        await AuthenticateAsync();
        var productId = await SeedProductAsync(stock: 20);
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 5);

        var created = await (await PostOrderAsync(addressId)).Content.ReadFromJsonAsync<OrderDetailDto>(Ct);

        var stockAfterOrder = await ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == productId).Select(p => p.Stock).FirstAsync());
        stockAfterOrder.ShouldBe(15);

        var response = await Client.PostAsync($"/api/orders/{created!.OrderNumber}/cancel", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cancelled = await response.Content.ReadFromJsonAsync<OrderDetailDto>(Ct);
        cancelled!.Status.ShouldBe(OrderStatus.Cancelled);

        var stockAfterCancel = await ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == productId).Select(p => p.Stock).FirstAsync());
        stockAfterCancel.ShouldBe(20);
    }

    [Fact]
    public async Task CancelOrder_WhenShipped_Returns400()
    {
        await AuthenticateAsync();
        var productId = await SeedProductAsync();
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 1);

        var created = await (await PostOrderAsync(addressId)).Content.ReadFromJsonAsync<OrderDetailDto>(Ct);

        await ExecuteDbAsync(async db =>
            await db.Orders.Where(o => o.OrderNumber == created!.OrderNumber)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Shipped)));

        var response = await Client.PostAsync($"/api/orders/{created!.OrderNumber}/cancel", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangeStatus_FromDeliveredToPending_Throws()
    {
        await AuthenticateAsync();
        var productId = await SeedProductAsync(stock: 10);
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 1);

        var created = await (await PostOrderAsync(addressId)).Content.ReadFromJsonAsync<OrderDetailDto>(Ct);

        await ExecuteDbAsync(async db =>
            await db.Orders.Where(o => o.OrderNumber == created!.OrderNumber)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Delivered)));

        // ChangeStatusAsync'in HTTP endpoint'i yok (Faz 8/11 servis seviyesinde
        // çağıracak) — burada da servis doğrudan çözülüyor.
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<OrderService>();

        await Should.ThrowAsync<InvalidOrderStatusTransitionException>(
            () => service.ChangeStatusAsync(created!.OrderNumber, OrderStatus.Pending, Ct));
    }

    [Fact]
    public async Task ChangeStatus_PaidToRefunded_RestoresStock()
    {
        await AuthenticateAsync();
        var productId = await SeedProductAsync(stock: 10);
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 4);

        var created = await (await PostOrderAsync(addressId)).Content.ReadFromJsonAsync<OrderDetailDto>(Ct);

        var stockAfterOrder = await ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == productId).Select(p => p.Stock).FirstAsync());
        stockAfterOrder.ShouldBe(6);

        await ExecuteDbAsync(async db =>
            await db.Orders.Where(o => o.OrderNumber == created!.OrderNumber)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Paid)));

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<OrderService>();
        await service.ChangeStatusAsync(created!.OrderNumber, OrderStatus.Refunded, Ct);

        var stockAfterRefund = await ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == productId).Select(p => p.Stock).FirstAsync());
        stockAfterRefund.ShouldBe(10);
    }

    // ═══════════════════════════════════════════════════════════
    // F. Kupon
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateOrder_WithValidCoupon_AppliesDiscountAndIncrementsUsedCount()
    {
        await AuthenticateAsync();
        var productId = await SeedProductAsync(price: 200m, stock: 10);
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 1);

        await SeedCouponAsync("ON10", CouponType.Percentage, 10m,
            minCartTotal: 100m, validFromDays: -1, validToDays: 30);
        (await Client.PostAsJsonAsync("/api/cart/coupon", new { code = "ON10" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await PostOrderAsync(addressId);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var order = await response.Content.ReadFromJsonAsync<OrderDetailDto>(Ct);
        order!.DiscountAmount.ShouldBe(20m);
        order.CouponCode.ShouldBe("ON10");
        order.Total.ShouldBe(209.90m);    // 200 - 20 + 29.90 (180 < 200 eşiği)

        var usedCount = await ExecuteDbAsync(db =>
            db.Coupons.Where(c => c.Code == "ON10").Select(c => c.UsedCount).FirstAsync());
        usedCount.ShouldBe(1);
    }

    [Fact]
    public async Task CreateOrder_WithExpiredCoupon_OrdersWithoutDiscount()
    {
        await AuthenticateAsync();
        var productId = await SeedProductAsync(price: 200m, stock: 10);
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 1);

        await SeedCouponAsync("SURELI10", CouponType.Percentage, 10m,
            minCartTotal: 100m, validFromDays: -5, validToDays: 5);
        (await Client.PostAsJsonAsync("/api/cart/coupon", new { code = "SURELI10" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // Sepete uygulandıktan SONRA kuponun süresi dolsun.
        await ExecuteDbAsync(async db =>
            await db.Coupons.Where(c => c.Code == "SURELI10")
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.ValidTo, DateTime.UtcNow.AddDays(-1))));

        var response = await PostOrderAsync(addressId);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var order = await response.Content.ReadFromJsonAsync<OrderDetailDto>(Ct);
        order!.DiscountAmount.ShouldBe(0m);
        order.CouponCode.ShouldBeNull();

        var usedCount = await ExecuteDbAsync(db =>
            db.Coupons.Where(c => c.Code == "SURELI10").Select(c => c.UsedCount).FirstAsync());
        usedCount.ShouldBe(0);
    }

    [Fact]
    public async Task CreateOrder_WhenCouponUsageLimitReached_OrdersWithoutDiscount()
    {
        var userId = await AuthenticateAsync();
        var productId = await SeedProductAsync(price: 200m, stock: 10);
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 1);

        // Kupon zaten limitte doğuyor — ApplyCouponAsync bunu reddederdi.
        // Müşterinin limit dolmadan ÖNCE uyguladığı, başkalarının aynı anda
        // tükettiği senaryoyu simüle etmek için sepetin kupon kodu doğrudan
        // veritabanında kuruluyor.
        await SeedCouponAsync("DOLU10", CouponType.Percentage, 10m,
            minCartTotal: 100m, validFromDays: -1, validToDays: 30, usageLimit: 1);
        await ExecuteDbAsync(async db =>
        {
            await db.Coupons.Where(c => c.Code == "DOLU10")
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedCount, 1));
            await db.Carts.Where(c => c.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.CouponCode, "DOLU10"));
        });

        var response = await PostOrderAsync(addressId);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var order = await response.Content.ReadFromJsonAsync<OrderDetailDto>(Ct);
        order!.DiscountAmount.ShouldBe(0m);
        order.CouponCode.ShouldBeNull();

        var usedCount = await ExecuteDbAsync(db =>
            db.Coupons.Where(c => c.Code == "DOLU10").Select(c => c.UsedCount).FirstAsync());
        usedCount.ShouldBe(1);
    }
}
