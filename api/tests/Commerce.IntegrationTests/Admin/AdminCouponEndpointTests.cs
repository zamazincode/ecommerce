using System.Net;
using System.Net.Http.Json;
using Commerce.Api.Features.Admin.Coupons;
using Commerce.Api.Persistence.Identity;
using Commerce.Domain.Common;
using Commerce.IntegrationTests.Infrastructure;
using Shouldly;

namespace Commerce.IntegrationTests.Admin;

public class AdminCouponEndpointTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<Guid> AuthenticateAsAdminAsync(string email = "admin-kupon@test.com")
        => AuthenticateAsync(email, role: AppRoles.Admin);

    private async Task<int> SeedProductAsync()
    {
        var id = 0;
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            var product = new ProductBuilder().WithName("Kupon Testi Kitabı").InCategory(category).Build();
            db.Products.Add(product);
            await db.SaveChangesAsync(Ct);
            id = product.Id;
        });
        return id;
    }

    [Fact]
    public async Task Create_Returns201_AndUpperCasesCode()
    {
        await AuthenticateAsAdminAsync();

        var response = await Client.PostAsJsonAsync("/api/admin/coupons", new
        {
            code = "yenikupon",
            type = CouponType.Percentage,
            value = 10m,
            minCartTotal = 0m,
            validFrom = "2026-08-01T00:00:00Z",
            validTo = "2026-09-01T00:00:00Z"
        }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<AdminCouponDto>(Ct);
        dto!.Code.ShouldBe("YENIKUPON");
    }

    [Fact]
    public async Task Create_WithTimezonelessDates_Returns201()
    {
        await AuthenticateAsAdminAsync();

        // 2.4 kilidi: AsUtc() normalizasyonu olmasaydı bu istek 500 dönerdi
        // (Npgsql, Kind=Unspecified'ı timestamptz'e yazmayı reddediyor).
        var response = await Client.PostAsJsonAsync("/api/admin/coupons", new
        {
            code = "SAATDILIMSIZ",
            type = CouponType.FixedAmount,
            value = 20m,
            minCartTotal = 0m,
            validFrom = "2026-08-01T00:00:00",
            validTo = "2026-09-01T00:00:00"
        }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_WithDuplicateCode_Returns409()
    {
        await AuthenticateAsAdminAsync();
        var body = new
        {
            code = "TEKRAR10",
            type = CouponType.Percentage,
            value = 10m,
            minCartTotal = 0m,
            validFrom = "2026-08-01T00:00:00Z",
            validTo = "2026-09-01T00:00:00Z"
        };
        (await Client.PostAsJsonAsync("/api/admin/coupons", body, Ct)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var second = await Client.PostAsJsonAsync("/api/admin/coupons", body, Ct);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_WithInvalidDateRange_Returns400()
    {
        await AuthenticateAsAdminAsync();

        var response = await Client.PostAsJsonAsync("/api/admin/coupons", new
        {
            code = "GECERSIZTARIH",
            type = CouponType.Percentage,
            value = 10m,
            minCartTotal = 0m,
            validFrom = "2026-09-01T00:00:00Z",
            validTo = "2026-08-01T00:00:00Z"
        }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_ReturnsUsageCounters()
    {
        await AuthenticateAsAdminAsync();
        await Client.PostAsJsonAsync("/api/admin/coupons", new
        {
            code = "SAYAC10",
            type = CouponType.Percentage,
            value = 10m,
            minCartTotal = 0m,
            validFrom = "2026-08-01T00:00:00Z",
            validTo = "2026-09-01T00:00:00Z"
        }, Ct);

        var list = await Client.GetFromJsonAsync<List<AdminCouponDto>>("/api/admin/coupons", Ct);

        var row = list!.Single(c => c.Code == "SAYAC10");
        row.UsedCount.ShouldBe(0);
        row.UsageLimit.ShouldBeNull();
        row.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Deactivate_ThenCartRejectsCoupon()
    {
        await AuthenticateAsAdminAsync();
        var createResponse = await Client.PostAsJsonAsync("/api/admin/coupons", new
        {
            code = "PASIFTEST",
            type = CouponType.Percentage,
            value = 10m,
            minCartTotal = 0m,
            validFrom = "2026-01-01T00:00:00Z",
            validTo = "2030-01-01T00:00:00Z"
        }, Ct);
        var coupon = await createResponse.Content.ReadFromJsonAsync<AdminCouponDto>(Ct);

        (await Client.PatchAsJsonAsync($"/api/admin/coupons/{coupon!.Id}", new { isActive = false }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var productId = await SeedProductAsync();
        await AuthenticateAsync("kupon-musteri@test.com");
        (await Client.PostAsJsonAsync("/api/cart/items", new { productId, quantity = 1 }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // Uçtan uca anlam: admin pasifleştirdi → sepet artık bu kuponu kabul etmiyor.
        var applyResponse = await Client.PostAsJsonAsync("/api/cart/coupon", new { code = "PASIFTEST" }, Ct);

        applyResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
