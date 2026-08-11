using System.Net;
using System.Net.Http.Json;
using Commerce.Api.Features.Admin.Reports;
using Commerce.Api.Features.Orders.Dtos;
using Commerce.Api.Persistence.Identity;
using Commerce.Domain.Common;
using Commerce.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Commerce.IntegrationTests.Admin;

public class AdminReportEndpointTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<Guid> AuthenticateAsAdminAsync(string email = "admin-rapor@test.com")
        => AuthenticateAsync(email, role: AppRoles.Admin);

    private async Task<int> SeedProductAsync(int stock = 20, decimal price = 100m)
    {
        var id = 0;
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            var product = new ProductBuilder()
                .WithName("Rapor Testi Kitabı").WithPrice(price).WithStock(stock).InCategory(category).Build();
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

    private async Task<string> CreatePendingOrderAsync(string customerEmail)
    {
        var productId = await SeedProductAsync();
        await AuthenticateAsync(customerEmail);
        var addressId = await CreateAddressAsync();
        (await Client.PostAsJsonAsync("/api/cart/items", new { productId, quantity = 1 }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await Client.PostAsJsonAsync("/api/orders", new { addressId }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var order = await response.Content.ReadFromJsonAsync<OrderDetailDto>(Ct);
        return order!.OrderNumber;
    }

    private async Task<HttpResponseMessage> AdminChangeStatusAsync(string orderNumber, OrderStatus status)
        => await Client.PatchAsJsonAsync($"/api/admin/orders/{orderNumber}/status", new { status }, Ct);

    // ═══════════════════════════════════════════════════════════
    // Dashboard
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task Dashboard_ReturnsProductAndOrderCounts()
    {
        await SeedProductAsync(stock: 0);
        await SeedProductAsync(stock: 3);
        await SeedProductAsync(stock: 50);
        await CreatePendingOrderAsync("dashboard-musteri@test.com");

        await AuthenticateAsAdminAsync();
        var dashboard = await Client.GetFromJsonAsync<DashboardSummaryDto>("/api/admin/dashboard", Ct);

        // 3 açıkça oluşturulan + siparişin kendi ürünü = 4.
        dashboard!.TotalProducts.ShouldBe(4);
        dashboard.OutOfStockProducts.ShouldBe(1);
        dashboard.LowStockProducts.ShouldBe(1);
        dashboard.TotalOrders.ShouldBe(1);
        dashboard.PendingOrders.ShouldBe(1);
    }

    [Fact]
    public async Task Dashboard_RevenueIgnoresCancelledOrders()
    {
        var orderNumber1 = await CreatePendingOrderAsync("ciro-1@test.com");
        var orderNumber2 = await CreatePendingOrderAsync("ciro-2@test.com");

        await AuthenticateAsAdminAsync();
        (await AdminChangeStatusAsync(orderNumber1, OrderStatus.Paid)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await AdminChangeStatusAsync(orderNumber2, OrderStatus.Paid)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await AdminChangeStatusAsync(orderNumber2, OrderStatus.Cancelled)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var order1Total = await ExecuteDbAsync(db =>
            db.Orders.Where(o => o.OrderNumber == orderNumber1).Select(o => o.Total).FirstAsync(Ct));

        var dashboard = await Client.GetFromJsonAsync<DashboardSummaryDto>("/api/admin/dashboard", Ct);

        dashboard!.TotalRevenue.ShouldBe(order1Total);
    }

    // ═══════════════════════════════════════════════════════════
    // Satış raporu
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task Sales_GroupByDay_ReturnsOneRowPerDay()
    {
        var orderNumber = await CreatePendingOrderAsync("rapor-gun@test.com");
        await AuthenticateAsAdminAsync();
        (await AdminChangeStatusAsync(orderNumber, OrderStatus.Paid)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await Client.GetFromJsonAsync<List<SalesReportItemDto>>(
            $"/api/admin/reports/sales?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}&groupBy=day", Ct);

        result!.Count.ShouldBe(1);
        result[0].Period.ShouldBe(today);
        result[0].OrderCount.ShouldBe(1);
    }

    [Fact]
    public async Task Sales_GroupByMonth_FoldsDaysIntoOneRow()
    {
        var orderNumber1 = await CreatePendingOrderAsync("rapor-ay-1@test.com");
        var orderNumber2 = await CreatePendingOrderAsync("rapor-ay-2@test.com");

        await AuthenticateAsAdminAsync();
        (await AdminChangeStatusAsync(orderNumber1, OrderStatus.Paid)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await AdminChangeStatusAsync(orderNumber2, OrderStatus.Paid)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // İkinci siparişi ayın İLK gününe taşı — ikisi FARKLI günlerde ama
        // AYNI ayda olsun (K6: gün satırları hafta/ay'a bellekte katlanıyor).
        var today = DateTime.UtcNow.Date;
        var earlierInMonth = new DateTime(today.Year, today.Month, 1, 12, 0, 0, DateTimeKind.Utc);
        await ExecuteDbAsync(db => db.Orders.Where(o => o.OrderNumber == orderNumber1)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.CreatedAt, earlierInMonth), Ct));

        var from = new DateOnly(today.Year, today.Month, 1);
        var to = DateOnly.FromDateTime(today);
        var result = await Client.GetFromJsonAsync<List<SalesReportItemDto>>(
            $"/api/admin/reports/sales?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&groupBy=month", Ct);

        result!.Count.ShouldBe(1);
        result[0].OrderCount.ShouldBe(2);
    }

    [Fact]
    public async Task Sales_WithRangeOver366Days_Returns400()
    {
        await AuthenticateAsAdminAsync();

        var response = await Client.GetAsync(
            "/api/admin/reports/sales?from=2020-01-01&to=2026-12-31&groupBy=month", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sales_WithNoOrders_ReturnsEmptyList()
    {
        await AuthenticateAsAdminAsync();

        var result = await Client.GetFromJsonAsync<List<SalesReportItemDto>>(
            "/api/admin/reports/sales?from=2020-01-01&to=2020-01-31&groupBy=day", Ct);

        result!.ShouldBeEmpty();
    }

    // ═══════════════════════════════════════════════════════════
    // Popüler arama terimleri
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task TopSearches_ReturnsMostFrequentTerm()
    {
        await Client.GetAsync("/api/search?q=sikcasorgu", Ct);
        await Client.GetAsync("/api/search?q=sikcasorgu", Ct);
        await Client.GetAsync("/api/search?q=nadirsorgu", Ct);

        await AuthenticateAsAdminAsync();
        var result = await Client.GetFromJsonAsync<List<TopSearchDto>>("/api/admin/reports/top-searches", Ct);

        result!.First().Term.ShouldBe("sikcasorgu");
        result![0].SearchCount.ShouldBe(2);
    }
}
