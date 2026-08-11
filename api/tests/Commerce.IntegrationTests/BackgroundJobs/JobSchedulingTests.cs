using System.Net;
using System.Net.Http.Json;
using Commerce.Api.Features.BackgroundJobs;
using Commerce.Api.Features.Cart;
using Commerce.Api.Features.Orders;
using Commerce.Api.Features.Orders.Dtos;
using Commerce.Api.Features.Payments.Dtos;
using Commerce.Domain.Common;
using Commerce.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Commerce.IntegrationTests.BackgroundJobs;

/// "Doğru anda doğru job kuyruğa girdi mi" — Testing'de gerçek Hangfire
/// storage'ına hiç dokunulmuyor (RecordingBackgroundJobClient), bu yüzden
/// job'ların KENDİSİ değil, sadece PLANLANMASI test ediliyor.
public class JobSchedulingTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly string _guestId = Guid.NewGuid().ToString();

    private async Task<int> SeedProductAsync(decimal price = 150m, int stock = 20)
    {
        var id = 0;
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            var product = new ProductBuilder()
                .WithName("Zamanlama Testi Kitabı").WithPrice(price).WithStock(stock).InCategory(category).Build();
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

    private async Task<OrderDetailDto> CreatePendingOrderAsync()
    {
        var productId = await SeedProductAsync();
        var addressId = await CreateAddressAsync();
        await AddToCartAsync(productId, 2);

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

    private async Task<HttpResponseMessage> PostCallbackAsync(string token)
    {
        var noRedirectClient = CreateNoRedirectClient();
        var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("token", token)]);
        return await noRedirectClient.PostAsync("/api/payments/callback", content, Ct);
    }

    // ═══════════════════════════════════════════════════════════
    // Sipariş onay / kargo bildirimi
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task SuccessfulPayment_EnqueuesOrderConfirmationJob()
    {
        await AuthenticateAsync();
        var order = await CreatePendingOrderAsync();
        var init = await InitializeAsync(order.OrderNumber);

        await PostCallbackAsync(init.ProviderReference);

        var orderId = await ExecuteDbAsync(db =>
            db.Orders.Where(o => o.OrderNumber == order.OrderNumber).Select(o => o.Id).FirstAsync(Ct));

        var jobs = Factory.BackgroundJobs.For<OrderNotificationJobs>(nameof(OrderNotificationJobs.SendOrderConfirmationAsync)).ToList();
        jobs.Count.ShouldBe(1);
        jobs[0].State.ShouldBe("Enqueued");
        jobs[0].Args[0].ShouldBe(orderId);
    }

    [Fact]
    public async Task SuccessfulPayment_SecondCallback_DoesNotEnqueueTwice()
    {
        await AuthenticateAsync();
        var order = await CreatePendingOrderAsync();
        var init = await InitializeAsync(order.OrderNumber);

        await PostCallbackAsync(init.ProviderReference);
        await PostCallbackAsync(init.ProviderReference);
        await PostCallbackAsync(init.ProviderReference);

        var jobs = Factory.BackgroundJobs.For<OrderNotificationJobs>(nameof(OrderNotificationJobs.SendOrderConfirmationAsync));
        jobs.Count().ShouldBe(1);
    }

    [Fact]
    public async Task FailedPayment_EnqueuesNoConfirmationJob()
    {
        await AuthenticateAsync();
        var order = await CreatePendingOrderAsync();
        var init = await InitializeAsync(order.OrderNumber);
        Factory.PaymentProvider.NextResultOverride = PaymentStatus.Failed;

        await PostCallbackAsync(init.ProviderReference);

        var jobs = Factory.BackgroundJobs.For<OrderNotificationJobs>(nameof(OrderNotificationJobs.SendOrderConfirmationAsync));
        jobs.ShouldBeEmpty();
    }

    // ═══════════════════════════════════════════════════════════
    // Sepet hatırlatma
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AddToCart_AsMember_Schedules24HourReminder()
    {
        var userId = await AuthenticateAsync();
        var productId = await SeedProductAsync();

        var before = DateTime.UtcNow;
        await AddToCartAsync(productId);

        var jobs = Factory.BackgroundJobs.For<CartReminderJobs>(nameof(CartReminderJobs.SendReminderAsync)).ToList();
        jobs.Count.ShouldBe(1);
        jobs[0].State.ShouldBe("Scheduled");
        jobs[0].Args[0].ShouldBe(userId);
        jobs[0].EnqueueAt.ShouldNotBeNull();
        jobs[0].EnqueueAt!.Value.ShouldBeInRange(
            before.AddHours(24).AddMinutes(-5), before.AddHours(24).AddMinutes(5));
    }

    [Fact]
    public async Task AddToCart_AsGuest_SchedulesNothing()
    {
        var productId = await SeedProductAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/cart/items")
        {
            Content = JsonContent.Create(new { productId, quantity = 1 })
        };
        request.Headers.Add(CartOwner.GuestHeader, _guestId);

        var response = await Client.SendAsync(request, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var jobs = Factory.BackgroundJobs.For<CartReminderJobs>(nameof(CartReminderJobs.SendReminderAsync));
        jobs.ShouldBeEmpty();
    }

    // ═══════════════════════════════════════════════════════════
    // Kargo bildirimi
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task ChangeStatusToShipped_EnqueuesShippedJob()
    {
        await AuthenticateAsync();
        var order = await CreatePendingOrderAsync();

        await ExecuteScopedAsync(async sp =>
        {
            var service = sp.GetRequiredService<OrderService>();
            await service.ChangeStatusAsync(order.OrderNumber, OrderStatus.Paid, ct: Ct);
            await service.ChangeStatusAsync(order.OrderNumber, OrderStatus.Preparing, ct: Ct);
            await service.ChangeStatusAsync(order.OrderNumber, OrderStatus.Shipped, ct: Ct);
        });

        var jobs = Factory.BackgroundJobs.For<OrderNotificationJobs>(nameof(OrderNotificationJobs.SendShippedNotificationAsync));
        jobs.Count().ShouldBe(1);
    }
}
