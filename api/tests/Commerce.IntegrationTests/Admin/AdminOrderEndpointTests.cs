using System.Net;
using System.Net.Http.Json;
using Commerce.Api.Common.Results;
using Commerce.Api.Features.Admin.Orders.Dtos;
using Commerce.Api.Features.BackgroundJobs;
using Commerce.Api.Features.Orders.Dtos;
using Commerce.Api.Persistence.Identity;
using Commerce.Domain.Common;
using Commerce.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Commerce.IntegrationTests.Admin;

public class AdminOrderEndpointTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<Guid> AuthenticateAsAdminAsync(string email = "admin-siparis@test.com")
        => AuthenticateAsync(email, role: AppRoles.Admin);

    private async Task<int> SeedProductAsync(int stock = 20, decimal price = 100m)
    {
        var id = 0;
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            var product = new ProductBuilder()
                .WithName("Sipariş Testi Kitabı").WithPrice(price).WithStock(stock).InCategory(category).Build();
            db.Products.Add(product);
            await db.SaveChangesAsync(Ct);
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
        response.EnsureSuccessStatusCode();
        var address = await response.Content.ReadFromJsonAsync<AddressDto>(Ct);
        return address!.Id;
    }

    private async Task AddToCartAsync(int productId, int quantity)
    {
        var response = await Client.PostAsJsonAsync("/api/cart/items", new { productId, quantity }, Ct);
        response.EnsureSuccessStatusCode();
    }

    /// Müşteri akışıyla GERÇEK bir sipariş oluşturur (Pending). Ödeme akışına
    /// girilmiyor — durum makinesi admin PATCH ucuyla test edildiği için
    /// Pending→Paid geçişi de doğrudan admin ucundan yapılıyor.
    private async Task<(string OrderNumber, int ProductId, string CustomerEmail)> CreatePendingOrderAsync(
        string customerEmail, int stock = 20)
    {
        var productId = await SeedProductAsync(stock);
        await AuthenticateAsync(customerEmail);
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 2);

        var response = await Client.PostAsJsonAsync("/api/orders", new { addressId }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var order = await response.Content.ReadFromJsonAsync<OrderDetailDto>(Ct);

        return (order!.OrderNumber, productId, customerEmail);
    }

    private async Task<HttpResponseMessage> AdminChangeStatusAsync(string orderNumber, OrderStatus status)
        => await Client.PatchAsJsonAsync($"/api/admin/orders/{orderNumber}/status", new { status }, Ct);

    // ═══════════════════════════════════════════════════════════
    // Liste / filtre
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task List_ReturnsOrdersWithCustomerEmail()
    {
        var (orderNumber, _, email) = await CreatePendingOrderAsync("liste-musteri@test.com");

        await AuthenticateAsAdminAsync();
        var result = await Client.GetFromJsonAsync<PagedResult<AdminOrderListDto>>("/api/admin/orders", Ct);

        var row = result!.Items.Where(i => i.OrderNumber == orderNumber).ShouldHaveSingleItem();
        row.CustomerEmail.ShouldBe(email);
    }

    [Fact]
    public async Task List_FilterByStatus_ReturnsOnlyMatching()
    {
        var (pendingNumber, _, _) = await CreatePendingOrderAsync("durum-filtre-1@test.com");
        var (paidNumber, _, _) = await CreatePendingOrderAsync("durum-filtre-2@test.com");

        await AuthenticateAsAdminAsync();
        (await AdminChangeStatusAsync(paidNumber, OrderStatus.Paid)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await Client.GetFromJsonAsync<PagedResult<AdminOrderListDto>>(
            "/api/admin/orders?status=Paid", Ct);

        result!.Items.ShouldContain(i => i.OrderNumber == paidNumber);
        result.Items.ShouldNotContain(i => i.OrderNumber == pendingNumber);
    }

    [Fact]
    public async Task List_FilterByDateRange_ReturnsTodaysOrder()
    {
        var (orderNumber, _, _) = await CreatePendingOrderAsync("tarih-filtre@test.com");

        await AuthenticateAsAdminAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Tarih tuzağının kilidi (plan 2.4): DateOnly? kullanılmazsa bu istek
        // 500 döner.
        var result = await Client.GetFromJsonAsync<PagedResult<AdminOrderListDto>>(
            $"/api/admin/orders?dateFrom={today:yyyy-MM-dd}&dateTo={today:yyyy-MM-dd}", Ct);

        result!.Items.ShouldContain(i => i.OrderNumber == orderNumber);
    }

    [Fact]
    public async Task List_SearchByEmail_IsCaseInsensitive()
    {
        var (orderNumber, _, email) = await CreatePendingOrderAsync("BuyukHarfli@test.com");

        await AuthenticateAsAdminAsync();
        var result = await Client.GetFromJsonAsync<PagedResult<AdminOrderListDto>>(
            $"/api/admin/orders?q={Uri.EscapeDataString(email.ToUpperInvariant())}", Ct);

        result!.Items.ShouldContain(i => i.OrderNumber == orderNumber);
    }

    // ═══════════════════════════════════════════════════════════
    // Durum değişimi
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateStatus_PaidToPreparing_Returns200()
    {
        var (orderNumber, _, _) = await CreatePendingOrderAsync("durum-mutlu-yol@test.com");

        await AuthenticateAsAdminAsync();
        (await AdminChangeStatusAsync(orderNumber, OrderStatus.Paid)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await AdminChangeStatusAsync(orderNumber, OrderStatus.Preparing);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<OrderDetailDto>(Ct);
        dto!.Status.ShouldBe(OrderStatus.Preparing);
    }

    [Fact]
    public async Task UpdateStatus_DeliveredToPending_Returns400()
    {
        var (orderNumber, _, _) = await CreatePendingOrderAsync("durum-makinesi@test.com");

        await AuthenticateAsAdminAsync();
        (await AdminChangeStatusAsync(orderNumber, OrderStatus.Paid)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await AdminChangeStatusAsync(orderNumber, OrderStatus.Preparing)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await AdminChangeStatusAsync(orderNumber, OrderStatus.Shipped)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await AdminChangeStatusAsync(orderNumber, OrderStatus.Delivered)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await AdminChangeStatusAsync(orderNumber, OrderStatus.Pending);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateStatus_ToShipped_EnqueuesShippedEmail()
    {
        var (orderNumber, _, _) = await CreatePendingOrderAsync("kargo-maili@test.com");

        await AuthenticateAsAdminAsync();
        (await AdminChangeStatusAsync(orderNumber, OrderStatus.Paid)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await AdminChangeStatusAsync(orderNumber, OrderStatus.Preparing)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await AdminChangeStatusAsync(orderNumber, OrderStatus.Shipped);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var jobs = Factory.BackgroundJobs.For<OrderNotificationJobs>(
            nameof(OrderNotificationJobs.SendShippedNotificationAsync));
        jobs.Count().ShouldBe(1);
    }

    [Fact]
    public async Task UpdateStatus_ToCancelled_RestoresStock()
    {
        var (orderNumber, productId, _) = await CreatePendingOrderAsync("iptal-stok@test.com", stock: 20);

        // Sipariş 2 adet aldı: stok 20 → 18.
        var stockAfterOrder = await ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == productId).Select(p => p.Stock).FirstAsync(Ct));
        stockAfterOrder.ShouldBe(18);

        await AuthenticateAsAdminAsync();
        (await AdminChangeStatusAsync(orderNumber, OrderStatus.Paid)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await AdminChangeStatusAsync(orderNumber, OrderStatus.Cancelled);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // K4'ün kanıtı: delege edilmeseydi stok geri gelmezdi.
        var stockAfterCancel = await ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == productId).Select(p => p.Stock).FirstAsync(Ct));
        stockAfterCancel.ShouldBe(20);
    }
}
