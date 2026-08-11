using System.Net;
using System.Net.Http.Json;
using Commerce.Api.Common.Results;
using Commerce.Api.Features.Admin.Audit;
using Commerce.Api.Features.Admin.Products.Dtos;
using Commerce.Api.Features.Orders.Dtos;
using Commerce.Api.Persistence.Identity;
using Commerce.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Commerce.IntegrationTests.Admin;

public class AdminAuditLogTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string FixedDescription = "Sabit Açıklama";

    private Task<Guid> AuthenticateAsAdminAsync(string email = "admin-denetim@test.com")
        => AuthenticateAsync(email, role: AppRoles.Admin);

    private async Task<int> SeedCategoryAsync()
    {
        var id = 0;
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            await db.SaveChangesAsync(Ct);
            id = category.Id;
        });
        return id;
    }

    /// Description SABİT tutulur ki "yalnızca değişen alanlar loglanıyor" testi
    /// Description'ın YANLIŞLIKLA değişmemesine güvenebilsin.
    private async Task<(int Id, int CategoryId)> SeedProductAsync()
    {
        var result = (Id: 0, CategoryId: 0);
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            var product = new ProductBuilder()
                .WithName("Mevcut Ürün").WithDescription(FixedDescription).WithPrice(10m).InCategory(category).Build();
            db.Products.Add(product);
            await db.SaveChangesAsync(Ct);
            result = (product.Id, category.Id);
        });
        return result;
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

    [Fact]
    public async Task CreateProduct_WritesCreatedAuditWithRealEntityId()
    {
        await AuthenticateAsAdminAsync();
        var categoryId = await SeedCategoryAsync();

        var response = await Client.PostAsJsonAsync("/api/admin/products",
            new { name = "Denetim Testi", price = 10m, stock = 1, categoryId }, Ct);
        var dto = await response.Content.ReadFromJsonAsync<AdminProductDetailDto>(Ct);

        var log = await ExecuteDbAsync(db => db.AuditLogs
            .Where(a => a.EntityType == "Product" && a.Action == "Created")
            .OrderByDescending(a => a.Id)
            .FirstAsync(Ct));

        // 2.5/A'nın düzeltmesi: EF'in geçici (negatif) anahtarı DEĞİL, gerçek Id.
        log.EntityId.ShouldBe(dto!.Id.ToString());
        int.Parse(log.EntityId!).ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task UpdateProduct_LogsOnlyChangedFields()
    {
        var (id, categoryId) = await SeedProductAsync();
        await AuthenticateAsAdminAsync();

        var response = await Client.PutAsJsonAsync($"/api/admin/products/{id}",
            new { name = "Mevcut Ürün", description = FixedDescription, price = 59.90m, categoryId, isActive = true }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var log = await ExecuteDbAsync(db => db.AuditLogs
            .Where(a => a.EntityType == "Product" && a.EntityId == id.ToString() && a.Action == "Updated")
            .OrderByDescending(a => a.Id)
            .FirstAsync(Ct));

        log.NewValues.ShouldNotBeNull();
        log.NewValues.ShouldContain("Price");
        log.NewValues.ShouldNotContain("Description");
    }

    [Fact]
    public async Task DeleteProduct_WritesAuditRow()
    {
        var (id, _) = await SeedProductAsync();
        await AuthenticateAsAdminAsync();

        (await Client.DeleteAsync($"/api/admin/products/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Takipli soft delete → "Updated" satırı (2.5/B'nin düzeltmesi:
        // ExecuteUpdateAsync kullansaydı bu satır HİÇ oluşmazdı).
        var log = await ExecuteDbAsync(db => db.AuditLogs
            .Where(a => a.EntityType == "Product" && a.EntityId == id.ToString() && a.Action == "Updated")
            .OrderByDescending(a => a.Id)
            .FirstAsync(Ct));

        log.NewValues.ShouldNotBeNull();
        log.NewValues.ShouldContain("DeletedAt");
    }

    [Fact]
    public async Task CustomerOrderFlow_WritesNoAuditRows()
    {
        var (productId, _) = await SeedProductAsync();
        await AuthenticateAsync("denetim-musteri@test.com");
        var addressId = await CreateAddressAsync();
        (await Client.PostAsJsonAsync("/api/cart/items", new { productId, quantity = 1 }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await Client.PostAsJsonAsync("/api/orders", new { addressId }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        // K2: yalnızca ADMIN yazmaları denetlenir — müşteri akışı hiç iz bırakmaz.
        var count = await ExecuteDbAsync(db => db.AuditLogs.CountAsync(Ct));
        count.ShouldBe(0);
    }

    [Fact]
    public async Task AuditLogs_NeverContainPasswordHash()
    {
        var (id, categoryId) = await SeedProductAsync();
        await AuthenticateAsAdminAsync();
        (await Client.PutAsJsonAsync($"/api/admin/products/{id}",
            new { name = "Değişti", description = FixedDescription, price = 20m, categoryId, isActive = true }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var rows = await ExecuteDbAsync(db => db.AuditLogs.ToListAsync(Ct));

        rows.ShouldNotBeEmpty();
        rows.ShouldAllBe(r =>
            (r.OldValues == null || !r.OldValues.Contains("PasswordHash")) &&
            (r.NewValues == null || !r.NewValues.Contains("PasswordHash")) &&
            (r.OldValues == null || !r.OldValues.Contains("TokenHash")) &&
            (r.NewValues == null || !r.NewValues.Contains("TokenHash")));
    }

    [Fact]
    public async Task AuditLogEndpoint_FiltersByEntityType()
    {
        var categoryId = await SeedCategoryAsync();
        await AuthenticateAsAdminAsync();
        (await Client.PostAsJsonAsync("/api/admin/products",
            new { name = "Filtre Testi", price = 10m, stock = 1, categoryId }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        (await Client.PostAsJsonAsync("/api/admin/categories", new { name = "Filtre Kategori" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var result = await Client.GetFromJsonAsync<PagedResult<AuditLogDto>>(
            "/api/admin/audit-logs?entityType=Category", Ct);

        result!.Items.ShouldNotBeEmpty();
        result.Items.ShouldAllBe(i => i.EntityType == "Category");
    }
}
