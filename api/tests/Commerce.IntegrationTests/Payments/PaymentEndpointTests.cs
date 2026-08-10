using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Commerce.Api.Features.Orders.Dtos;
using Commerce.Api.Features.Payments.Dtos;
using Commerce.Domain.Common;
using Commerce.Domain.Orders;
using Commerce.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Commerce.IntegrationTests.Payments;

public class PaymentEndpointTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ═══════════════════════════════════════════════════════════
    // Kurulum yardımcıları — Faz 7'nin OrderEndpointTests kalıbının aynısı
    // ═══════════════════════════════════════════════════════════

    private async Task<int> SeedProductAsync(decimal price = 150m, int stock = 20)
    {
        var id = 0;
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            var product = new ProductBuilder()
                .WithName("Ödeme Testi Kitabı").WithPrice(price).WithStock(stock).InCategory(category).Build();
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
        string code, CouponType type, decimal value, decimal minCartTotal, int validFromDays, int validToDays)
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

    /// Ödemeye hazır bir sipariş: varsayılan 2 × 150₺ = 300₺ ara toplam,
    /// 300 ≥ 200 → kargo bedava → Total = 300.00.
    private async Task<OrderDetailDto> CreatePendingOrderAsync(
        int stock = 20, decimal price = 150m, int quantity = 2)
    {
        var productId = await SeedProductAsync(price, stock);
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, quantity);

        var response = await Client.PostAsJsonAsync("/api/orders", new { addressId }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<OrderDetailDto>(Ct))!;
    }

    private async Task<PaymentInitializedDto> InitializeAsync(string orderNumber)
    {
        var response = await Client.PostAsJsonAsync("/api/payments/initialize", new { orderNumber }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<PaymentInitializedDto>(Ct))!;
    }

    /// Tarayıcının gönderdiği form-encoded POST. AllowAutoRedirect varsayılanı
    /// TRUE olduğu için (ölçüm 2.1) YÖNLENDİRMEYİ TAKİP ETMEYEN bir istemci
    /// kullanılıyor — yoksa 302 yerine TestServer'ın 404'ü görülür.
    private async Task<HttpResponseMessage> PostCallbackAsync(string? token)
    {
        var noRedirectClient = CreateNoRedirectClient();
        var content = token is null
            ? new FormUrlEncodedContent([])
            : new FormUrlEncodedContent([new KeyValuePair<string, string>("token", token)]);

        return await noRedirectClient.PostAsync("/api/payments/callback", content, Ct);
    }

    /// Sunucudan sunucuya JSON webhook — iyzico'nun gerçek gövde biçimi.
    private async Task<HttpResponseMessage> PostWebhookJsonAsync(string token)
    {
        var body = JsonSerializer.Serialize(new
        {
            iyziEventType = "CHECKOUTFORM_AUTH",
            token,
            status = "SUCCESS"
        });
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        return await Client.PostAsync("/api/payments/webhook", content, Ct);
    }

    private static string Durum(HttpResponseMessage response)
    {
        var location = response.Headers.Location?.ToString() ?? "";
        var match = Regex.Match(location, "durum=([^&]+)");
        return match.Success ? match.Groups[1].Value : "";
    }

    // ═══════════════════════════════════════════════════════════
    // A. Başlatma
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task Initialize_ForOwnPendingOrder_ReturnsCheckoutContent()
    {
        await AuthenticateAsync();
        var order = await CreatePendingOrderAsync();

        var result = await InitializeAsync(order.OrderNumber);

        result.ProviderReference.ShouldNotBeNullOrWhiteSpace();
        result.CheckoutContent.ShouldNotBeNullOrWhiteSpace();
        result.Amount.ShouldBe(300.00m);
    }

    [Fact]
    public async Task Initialize_PersistsPendingPaymentWithRawResponse()
    {
        await AuthenticateAsync();
        var order = await CreatePendingOrderAsync();

        await InitializeAsync(order.OrderNumber);

        var payment = await ExecuteDbAsync(db => db.Payments.FirstAsync());
        payment.Status.ShouldBe(PaymentStatus.Pending);
        payment.Amount.ShouldBe(300.00m);
        payment.RawResponse.ShouldNotBeNullOrWhiteSpace();
        payment.ProviderReference.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Initialize_SendsLiraAmount_NotMinorUnits()
    {
        await AuthenticateAsync();
        var order = await CreatePendingOrderAsync();

        await InitializeAsync(order.OrderNumber);

        Factory.PaymentProvider.InitializedRequests.Single().Amount.ShouldBe(300.00m);   // 30000 DEĞİL
    }

    [Fact]
    public async Task Initialize_BasketItemsSumExactlyToAmount()
    {
        await AuthenticateAsync();
        var order = await CreatePendingOrderAsync();

        await InitializeAsync(order.OrderNumber);

        var sent = Factory.PaymentProvider.InitializedRequests.Single();
        sent.Items.Sum(i => i.Price).ShouldBe(sent.Amount);
    }

    [Fact]
    public async Task Initialize_WithCouponAndShipping_BasketStillSumsToTotal()
    {
        await AuthenticateAsync();
        var productId = await SeedProductAsync(price: 100m, stock: 10);
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 1);

        await SeedCouponAsync("ON10", CouponType.Percentage, 10m, minCartTotal: 50m, validFromDays: -1, validToDays: 30);
        (await Client.PostAsJsonAsync("/api/cart/coupon", new { code = "ON10" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var orderResponse = await Client.PostAsJsonAsync("/api/orders", new { addressId }, Ct);
        var order = (await orderResponse.Content.ReadFromJsonAsync<OrderDetailDto>(Ct))!;
        order.Total.ShouldBe(119.90m);   // 100 - 10 indirim + 29.90 kargo (90 < 200 eşiği)

        // Kılavuzun kodu burada kalemleri SubTotal'dan (100) üretirdi — iyzico
        // Price=119.90 ile basketItems=100 uyuşmazlığını reddederdi.
        await InitializeAsync(order.OrderNumber);

        var sent = Factory.PaymentProvider.InitializedRequests.Single();
        sent.Amount.ShouldBe(119.90m);
        sent.Items.Sum(i => i.Price).ShouldBe(119.90m);
    }

    [Fact]
    public async Task Initialize_SendsClientIp()
    {
        await AuthenticateAsync();
        var order = await CreatePendingOrderAsync();

        await InitializeAsync(order.OrderNumber);

        Factory.PaymentProvider.InitializedRequests.Single().Buyer.Ip.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Initialize_ForAnotherUsersOrder_Returns403()
    {
        await AuthenticateAsync("sahibi@test.com");
        var order = await CreatePendingOrderAsync();

        ClearAuthentication();
        await AuthenticateAsync("saldirgan@test.com");

        var response = await Client.PostAsJsonAsync(
            "/api/payments/initialize", new { orderNumber = order.OrderNumber }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Initialize_ForAlreadyPaidOrder_Returns400()
    {
        await AuthenticateAsync();
        var order = await CreatePendingOrderAsync();

        await ExecuteDbAsync(async db =>
            await db.Orders.Where(o => o.OrderNumber == order.OrderNumber)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Paid)));

        var response = await Client.PostAsJsonAsync(
            "/api/payments/initialize", new { orderNumber = order.OrderNumber }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Initialize_ForUnknownOrderNumber_Returns404()
    {
        await AuthenticateAsync();

        var response = await Client.PostAsJsonAsync(
            "/api/payments/initialize", new { orderNumber = "ORD-20260101-ZZZZZZ" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Initialize_WithoutToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/payments/initialize", new { orderNumber = "ORD-20260101-ZZZZZZ" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Initialize_WhenProviderFails_Returns400AndStoresFailedPayment()
    {
        await AuthenticateAsync();
        var order = await CreatePendingOrderAsync();
        Factory.PaymentProvider.FailInitialize = true;

        var response = await Client.PostAsJsonAsync(
            "/api/payments/initialize", new { orderNumber = order.OrderNumber }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var payment = await ExecuteDbAsync(db => db.Payments.FirstAsync());
        payment.Status.ShouldBe(PaymentStatus.Failed);
        payment.RawResponse.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Initialize_TwiceForSameOrder_CreatesTwoPendingPayments()
    {
        await AuthenticateAsync();
        var order = await CreatePendingOrderAsync();

        await InitializeAsync(order.OrderNumber);
        await InitializeAsync(order.OrderNumber);

        var payments = await ExecuteDbAsync(db => db.Payments.ToListAsync());
        payments.Count.ShouldBe(2);
        payments.ShouldAllBe(p => p.Status == PaymentStatus.Pending);
        payments[0].ProviderReference.ShouldNotBe(payments[1].ProviderReference);
    }

    // ═══════════════════════════════════════════════════════════
    // B. Callback
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task Callback_WithSuccessfulPayment_RedirectsAndMarksOrderPaid()
    {
        await AuthenticateAsync();
        var order = await CreatePendingOrderAsync();
        var init = await InitializeAsync(order.OrderNumber);

        var response = await PostCallbackAsync(init.ProviderReference);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        Durum(response).ShouldBe("basarili");
        response.Headers.Location!.ToString().ShouldContain(order.OrderNumber);

        var persisted = await ExecuteDbAsync(db =>
            db.Orders.FirstAsync(o => o.OrderNumber == order.OrderNumber));
        persisted.Status.ShouldBe(OrderStatus.Paid);

        var payment = await ExecuteDbAsync(db => db.Payments.FirstAsync());
        payment.Status.ShouldBe(PaymentStatus.Succeeded);
        payment.ProviderTransactionId.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Callback_WithFailedPayment_LeavesOrderPendingAndStockReserved()
    {
        await AuthenticateAsync();
        var order = await CreatePendingOrderAsync(stock: 20);
        var init = await InitializeAsync(order.OrderNumber);

        Factory.PaymentProvider.NextResultOverride = PaymentStatus.Failed;

        var response = await PostCallbackAsync(init.ProviderReference);

        Durum(response).ShouldBe("basarisiz");

        var persisted = await ExecuteDbAsync(db =>
            db.Orders.FirstAsync(o => o.OrderNumber == order.OrderNumber));
        persisted.Status.ShouldBe(OrderStatus.Pending);

        // Stok REZERVE kalır — Faz 9'daki job süresi dolunca serbest bırakacak.
        var stock = await ExecuteDbAsync(db => db.Products.Select(p => p.Stock).FirstAsync());
        stock.ShouldBe(18);

        var payment = await ExecuteDbAsync(db => db.Payments.FirstAsync());
        payment.Status.ShouldBe(PaymentStatus.Failed);
    }

    [Fact]
    public async Task Callback_ReceivedThreeTimes_ProcessesOrderOnlyOnce()
    {
        await AuthenticateAsync();
        var order = await CreatePendingOrderAsync();
        var init = await InitializeAsync(order.OrderNumber);

        var r1 = await PostCallbackAsync(init.ProviderReference);
        var r2 = await PostCallbackAsync(init.ProviderReference);
        var r3 = await PostCallbackAsync(init.ProviderReference);

        foreach (var r in new[] { r1, r2, r3 })
            r.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        var paymentCount = await ExecuteDbAsync(db => db.Payments.CountAsync());
        paymentCount.ShouldBe(1);

        var succeededCount = await ExecuteDbAsync(db =>
            db.Payments.CountAsync(p => p.Status == PaymentStatus.Succeeded));
        succeededCount.ShouldBe(1);

        var persisted = await ExecuteDbAsync(db =>
            db.Orders.FirstAsync(o => o.OrderNumber == order.OrderNumber));
        persisted.Status.ShouldBe(OrderStatus.Paid);
    }

    [Fact]
    public async Task Callback_AfterSuccess_DoesNotAskProviderAgain()
    {
        await AuthenticateAsync();
        var order = await CreatePendingOrderAsync();
        var init = await InitializeAsync(order.OrderNumber);

        await PostCallbackAsync(init.ProviderReference);
        await PostCallbackAsync(init.ProviderReference);
        await PostCallbackAsync(init.ProviderReference);

        // K2 kısa devresi: zaten Succeeded olan ödeme için sağlayıcıya tekrar
        // sorulmuyor — amplifikasyon ve gereksiz ağ trafiği önleniyor.
        Factory.PaymentProvider.VerifyCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Callback_WithUnknownToken_DoesNotChangeOrderAndDoesNotCallProvider()
    {
        await AuthenticateAsync();
        var order = await CreatePendingOrderAsync();

        var response = await PostCallbackAsync("uydurma-token-12345");

        Durum(response).ShouldBe("gecersiz");

        var persisted = await ExecuteDbAsync(db =>
            db.Orders.FirstAsync(o => o.OrderNumber == order.OrderNumber));
        persisted.Status.ShouldBe(OrderStatus.Pending);

        // K2: sahte token yerel kayıtta bulunamadığı için sağlayıcıya HİÇ
        // sorulmadı (kılavuzda 1 olurdu — amplifikasyon).
        Factory.PaymentProvider.VerifyCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task Callback_WithoutToken_RedirectsToInvalidResult()
    {
        var response = await PostCallbackAsync(null);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        Durum(response).ShouldBe("gecersiz");
    }

    [Fact]
    public async Task Callback_WithAmountMismatch_KeepsOrderPendingAndRecordsPayment()
    {
        await AuthenticateAsync();
        var order = await CreatePendingOrderAsync();
        var init = await InitializeAsync(order.OrderNumber);

        Factory.PaymentProvider.PaidAmountOverride = 1m;

        var response = await PostCallbackAsync(init.ProviderReference);

        Durum(response).ShouldBe("inceleniyor");

        var persisted = await ExecuteDbAsync(db =>
            db.Orders.FirstAsync(o => o.OrderNumber == order.OrderNumber));
        persisted.Status.ShouldBe(OrderStatus.Pending);   // sipariş DEĞİŞMEDİ

        // Para GERÇEKTEN çekildi — kayıt kaybolmadı, ProviderTransactionId elde.
        var payment = await ExecuteDbAsync(db => db.Payments.FirstAsync());
        payment.Status.ShouldBe(PaymentStatus.Succeeded);
        payment.ProviderTransactionId.ShouldNotBeNullOrWhiteSpace();
    }

    // ═══════════════════════════════════════════════════════════
    // C. Webhook
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task Webhook_WithJsonBody_MarksOrderPaid()
    {
        await AuthenticateAsync();
        var order = await CreatePendingOrderAsync();
        var init = await InitializeAsync(order.OrderNumber);

        // Kılavuzun ReadFormAsync'i JSON gövdede patlar, catch yutar, 200
        // döner ama sipariş HİÇ değişmezdi. PaymentCallbackReader ikisini de okur.
        var response = await PostWebhookJsonAsync(init.ProviderReference);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var persisted = await ExecuteDbAsync(db =>
            db.Orders.FirstAsync(o => o.OrderNumber == order.OrderNumber));
        persisted.Status.ShouldBe(OrderStatus.Paid);
    }

    [Fact]
    public async Task Webhook_WithFormBody_MarksOrderPaid()
    {
        await AuthenticateAsync();
        var order = await CreatePendingOrderAsync();
        var init = await InitializeAsync(order.OrderNumber);

        var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("token", init.ProviderReference)]);
        var response = await Client.PostAsync("/api/payments/webhook", content, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var persisted = await ExecuteDbAsync(db =>
            db.Orders.FirstAsync(o => o.OrderNumber == order.OrderNumber));
        persisted.Status.ShouldBe(OrderStatus.Paid);
    }

    [Fact]
    public async Task Webhook_ReceivedTwice_IsIdempotent()
    {
        await AuthenticateAsync();
        var order = await CreatePendingOrderAsync();
        var init = await InitializeAsync(order.OrderNumber);

        (await PostWebhookJsonAsync(init.ProviderReference)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PostWebhookJsonAsync(init.ProviderReference)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var succeededCount = await ExecuteDbAsync(db =>
            db.Payments.CountAsync(p => p.Status == PaymentStatus.Succeeded));
        succeededCount.ShouldBe(1);
    }

    [Fact]
    public async Task Webhook_WithUnparsableBody_Returns200()
    {
        // 500 dönersek sağlayıcı aynı bildirimi saatlerce tekrar gönderir.
        var content = new StringContent("<<bozuk>>", Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/api/payments/webhook", content, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ═══════════════════════════════════════════════════════════
    // D. Durum sorgusu
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetStatus_AfterSuccessfulPayment_ReturnsPaid()
    {
        await AuthenticateAsync();
        var order = await CreatePendingOrderAsync();
        var init = await InitializeAsync(order.OrderNumber);
        await PostCallbackAsync(init.ProviderReference);

        var status = await Client.GetFromJsonAsync<PaymentStatusDto>(
            $"/api/payments/{order.OrderNumber}/status", Ct);

        status!.OrderStatus.ShouldBe(OrderStatus.Paid);
        status.PaymentStatus.ShouldBe(PaymentStatus.Succeeded);
        status.Amount.ShouldBe(300.00m);
        status.UpdatedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetStatus_ForAnotherUsersOrder_Returns403()
    {
        await AuthenticateAsync("sahibi@test.com");
        var order = await CreatePendingOrderAsync();

        ClearAuthentication();
        await AuthenticateAsync("baskasi@test.com");

        var response = await Client.GetAsync($"/api/payments/{order.OrderNumber}/status", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetStatus_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/payments/ORD-20260101-AAAAAA/status", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetStatus_ForUnknownOrder_Returns404()
    {
        await AuthenticateAsync();

        var response = await Client.GetAsync("/api/payments/ORD-20260101-ZZZZZZ/status", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ═══════════════════════════════════════════════════════════
    // E. Faz 7 ile uyum
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task PaidOrder_CanStillBeCancelled_AndStockRestored()
    {
        await AuthenticateAsync();
        var order = await CreatePendingOrderAsync(stock: 20);
        var init = await InitializeAsync(order.OrderNumber);
        await PostCallbackAsync(init.ProviderReference);

        var response = await Client.PostAsync($"/api/orders/{order.OrderNumber}/cancel", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cancelled = await response.Content.ReadFromJsonAsync<OrderDetailDto>(Ct);
        cancelled!.Status.ShouldBe(OrderStatus.Cancelled);

        var stock = await ExecuteDbAsync(db => db.Products.Select(p => p.Stock).FirstAsync());
        stock.ShouldBe(20);
    }
}
