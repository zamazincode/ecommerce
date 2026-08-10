using System.Net;
using System.Net.Http.Json;
using Commerce.Api.Features.BackgroundJobs;
using Commerce.Api.Features.Orders.Dtos;
using Commerce.Domain.Common;
using Commerce.Domain.Orders;
using Commerce.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Commerce.IntegrationTests.BackgroundJobs;

public class OrderExpiryJobsTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<int> SeedProductAsync(decimal price = 150m, int stock = 20)
    {
        var id = 0;
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            var product = new ProductBuilder()
                .WithName("Süresi Dolan Sipariş Testi").WithPrice(price).WithStock(stock).InCategory(category).Build();
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
            title = "Ev", fullName = "Ali Veli", phone = "5551112233",
            city = "İstanbul", district = "Kadıköy", fullAddress = "Moda Caddesi No 1 Daire 5",
            isDefault = true
        }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var address = await response.Content.ReadFromJsonAsync<AddressDto>(Ct);
        return address!.Id;
    }

    private async Task AddToCartAsync(int productId, int quantity = 1)
    {
        var response = await Client.PostAsJsonAsync("/api/cart/items", new { productId, quantity }, Ct);
        response.EnsureSuccessStatusCode();
    }

    /// 2 × 150₺'lik bir Pending sipariş oluşturur, stoktan 2 düşürür (20 -> 18).
    private async Task<(string OrderNumber, int ProductId)> CreatePendingOrderAsync(int stock = 20)
    {
        var productId = await SeedProductAsync(stock: stock);
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 2);

        var response = await Client.PostAsJsonAsync("/api/orders", new { addressId }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var order = (await response.Content.ReadFromJsonAsync<OrderDetailDto>(Ct))!;

        return (order.OrderNumber, productId);
    }

    private Task SetCreatedAtAsync(string orderNumber, DateTime createdAt)
        => ExecuteDbAsync(async db =>
            await db.Orders.Where(o => o.OrderNumber == orderNumber)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.CreatedAt, createdAt), Ct));

    private Task RunExpiryAsync()
        => ExecuteScopedAsync(sp =>
            sp.GetRequiredService<OrderExpiryJobs>().CancelExpiredPendingOrdersAsync());

    [Fact]
    public async Task CancelExpiredPendingOrders_CancelsOldOrder_AndRestoresStock()
    {
        await AuthenticateAsync();
        var (orderNumber, productId) = await CreatePendingOrderAsync(stock: 20);
        await SetCreatedAtAsync(orderNumber, DateTime.UtcNow.AddMinutes(-31));

        await RunExpiryAsync();

        var status = await ExecuteDbAsync(db =>
            db.Orders.Where(o => o.OrderNumber == orderNumber).Select(o => o.Status).FirstAsync(Ct));
        status.ShouldBe(OrderStatus.Cancelled);

        var stock = await ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == productId).Select(p => p.Stock).FirstAsync(Ct));
        stock.ShouldBe(20);
    }

    [Fact]
    public async Task CancelExpiredPendingOrders_LeavesRecentOrderAlone()
    {
        await AuthenticateAsync();
        var (orderNumber, productId) = await CreatePendingOrderAsync(stock: 20);
        await SetCreatedAtAsync(orderNumber, DateTime.UtcNow.AddMinutes(-5));

        await RunExpiryAsync();

        var status = await ExecuteDbAsync(db =>
            db.Orders.Where(o => o.OrderNumber == orderNumber).Select(o => o.Status).FirstAsync(Ct));
        status.ShouldBe(OrderStatus.Pending);

        var stock = await ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == productId).Select(p => p.Stock).FirstAsync(Ct));
        stock.ShouldBe(18);
    }

    [Fact]
    public async Task CancelExpiredPendingOrders_SkipsOrderWithSucceededPayment()
    {
        await AuthenticateAsync();
        var (orderNumber, _) = await CreatePendingOrderAsync(stock: 20);
        await SetCreatedAtAsync(orderNumber, DateTime.UtcNow.AddMinutes(-31));

        // Faz 8 senaryosu: para GERÇEKTEN çekildi ama tutar uyuşmazlığı yüzünden
        // sipariş elle inceleme bekleyerek Pending bırakıldı — job buna DOKUNMAMALI.
        await ExecuteDbAsync(async db =>
        {
            var orderId = await db.Orders.Where(o => o.OrderNumber == orderNumber).Select(o => o.Id).FirstAsync(Ct);
            db.Payments.Add(new Payment
            {
                OrderId = orderId,
                Provider = "fake",
                ProviderReference = "ref-" + orderNumber,
                ProviderTransactionId = "txn-" + orderNumber,
                Status = PaymentStatus.Succeeded,
                Amount = 300m,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(Ct);
        });

        await RunExpiryAsync();

        var status = await ExecuteDbAsync(db =>
            db.Orders.Where(o => o.OrderNumber == orderNumber).Select(o => o.Status).FirstAsync(Ct));
        status.ShouldBe(OrderStatus.Pending);
    }

    [Fact]
    public async Task CancelExpiredPendingOrders_LeavesPaidOrderAlone()
    {
        await AuthenticateAsync();
        var (orderNumber, productId) = await CreatePendingOrderAsync(stock: 20);
        await SetCreatedAtAsync(orderNumber, DateTime.UtcNow.AddMinutes(-31));

        await ExecuteDbAsync(async db =>
            await db.Orders.Where(o => o.OrderNumber == orderNumber)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Paid), Ct));

        await RunExpiryAsync();

        var status = await ExecuteDbAsync(db =>
            db.Orders.Where(o => o.OrderNumber == orderNumber).Select(o => o.Status).FirstAsync(Ct));
        status.ShouldBe(OrderStatus.Paid);

        // Stok İKİNCİ kez iade edilmedi — hâlâ sipariş anında düşürülen değerde.
        var stock = await ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == productId).Select(p => p.Stock).FirstAsync(Ct));
        stock.ShouldBe(18);
    }
}
