using System.Net;
using System.Net.Http.Json;
using Commerce.Api.Features.BackgroundJobs;
using Commerce.Api.Features.Orders.Dtos;
using Commerce.Domain.Common;
using Commerce.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Commerce.IntegrationTests.BackgroundJobs;

public class OrderNotificationJobsTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
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
                .WithName("Bildirim Testi Kitabı").WithPrice(price).WithStock(stock).InCategory(category).Build();
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

    private async Task AddToCartAsync(int productId, int quantity = 1)
    {
        var response = await Client.PostAsJsonAsync("/api/cart/items", new { productId, quantity }, Ct);
        response.EnsureSuccessStatusCode();
    }

    private async Task<(string OrderNumber, int OrderId)> CreatePendingOrderAsync()
    {
        var productId = await SeedProductAsync();
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 2);

        var response = await Client.PostAsJsonAsync("/api/orders", new { addressId }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var order = (await response.Content.ReadFromJsonAsync<OrderDetailDto>(Ct))!;

        var orderId = await ExecuteDbAsync(db =>
            db.Orders.Where(o => o.OrderNumber == order.OrderNumber).Select(o => o.Id).FirstAsync(Ct));

        return (order.OrderNumber, orderId);
    }

    private Task RunConfirmationAsync(int orderId)
        => ExecuteScopedAsync(sp =>
            sp.GetRequiredService<OrderNotificationJobs>().SendOrderConfirmationAsync(orderId));

    private Task RunShippedAsync(int orderId)
        => ExecuteScopedAsync(sp =>
            sp.GetRequiredService<OrderNotificationJobs>().SendShippedNotificationAsync(orderId));

    private Task RunCancelledAsync(int orderId)
        => ExecuteScopedAsync(sp =>
            sp.GetRequiredService<OrderNotificationJobs>().SendCancelledNotificationAsync(orderId));

    [Fact]
    public async Task SendOrderConfirmation_SendsMailAndStampsSentAt()
    {
        await AuthenticateAsync();
        var (orderNumber, orderId) = await CreatePendingOrderAsync();

        await RunConfirmationAsync(orderId);

        Factory.EmailService.SentEmails.Count.ShouldBe(1);
        Factory.EmailService.SentEmails.ShouldContain(e =>
            e.To == "musteri@test.com" && e.Subject.Contains(orderNumber));

        var sentAt = await ExecuteDbAsync(db =>
            db.Orders.Where(o => o.OrderNumber == orderNumber).Select(o => o.ConfirmationEmailSentAt).FirstAsync(Ct));
        sentAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task SendOrderConfirmation_RunTwice_SendsOnlyOnce()
    {
        await AuthenticateAsync();
        var (_, orderId) = await CreatePendingOrderAsync();

        await RunConfirmationAsync(orderId);
        await RunConfirmationAsync(orderId);

        Factory.EmailService.SentEmails.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SendOrderConfirmation_ForUnknownOrderId_DoesNothing()
    {
        await Should.NotThrowAsync(() => RunConfirmationAsync(999_999));

        Factory.EmailService.SentEmails.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendOrderConfirmation_WhenMailThrows_DoesNotStampSentAt()
    {
        await AuthenticateAsync();
        var (orderNumber, orderId) = await CreatePendingOrderAsync();
        Factory.EmailService.ThrowOnSend = true;

        await Should.ThrowAsync<Exception>(() => RunConfirmationAsync(orderId));

        // K4'ün sırası: mail patladıysa damga atılmamalı, Hangfire retry edebilsin.
        var sentAt = await ExecuteDbAsync(db =>
            db.Orders.Where(o => o.OrderNumber == orderNumber).Select(o => o.ConfirmationEmailSentAt).FirstAsync(Ct));
        sentAt.ShouldBeNull();
    }

    [Fact]
    public async Task SendShippedNotification_RunTwice_SendsOnlyOnce()
    {
        await AuthenticateAsync();
        var (orderNumber, orderId) = await CreatePendingOrderAsync();

        // Shipped'e geçebilmek için Pending -> Paid -> Preparing -> Shipped.
        await ExecuteDbAsync(async db =>
            await db.Orders.Where(o => o.OrderNumber == orderNumber)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Shipped), Ct));

        await RunShippedAsync(orderId);
        await RunShippedAsync(orderId);

        Factory.EmailService.SentEmails.Count.ShouldBe(1);
        var sentAt = await ExecuteDbAsync(db =>
            db.Orders.Where(o => o.OrderNumber == orderNumber).Select(o => o.ShippedEmailSentAt).FirstAsync(Ct));
        sentAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task SendCancelledNotification_SendsMailAndStampsSentAt()
    {
        await AuthenticateAsync();
        var (orderNumber, orderId) = await CreatePendingOrderAsync();

        await ExecuteDbAsync(async db =>
            await db.Orders.Where(o => o.OrderNumber == orderNumber)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Cancelled), Ct));

        await RunCancelledAsync(orderId);

        Factory.EmailService.SentEmails.Count.ShouldBe(1);
        Factory.EmailService.SentEmails.ShouldContain(e =>
            e.To == "musteri@test.com" && e.Subject.Contains(orderNumber));

        var sentAt = await ExecuteDbAsync(db =>
            db.Orders.Where(o => o.OrderNumber == orderNumber).Select(o => o.CancelledEmailSentAt).FirstAsync(Ct));
        sentAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task SendCancelledNotification_RunTwice_SendsOnlyOnce()
    {
        await AuthenticateAsync();
        var (_, orderId) = await CreatePendingOrderAsync();

        await RunCancelledAsync(orderId);
        await RunCancelledAsync(orderId);

        Factory.EmailService.SentEmails.Count.ShouldBe(1);
    }
}
