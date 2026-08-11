using System.Net;
using System.Net.Http.Json;
using Commerce.Api.Common.Results;
using Commerce.Api.Features.Admin.Products.Dtos;
using Commerce.Api.Features.Catalog.Dtos;
using Commerce.Api.Features.Search;
using Commerce.Api.Persistence;
using Commerce.Api.Persistence.Identity;
using Commerce.Domain.Catalog;
using Commerce.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Commerce.IntegrationTests.Admin;

public class AdminProductEndpointTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<Guid> AuthenticateAsAdminAsync(string email = "admin-urun@test.com")
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

    private async Task<int> SeedPublisherAsync(string name)
    {
        var id = 0;
        await ExecuteDbAsync(async db =>
        {
            var publisher = new Publisher { Name = name, Slug = name.ToLowerInvariant().Replace(" ", "-") };
            db.Publishers.Add(publisher);
            await db.SaveChangesAsync(Ct);
            id = publisher.Id;
        });
        return id;
    }

    private async Task<(int Id, string Slug)> SeedProductViaDbAsync(
        string name = "Mevcut Ürün", decimal price = 100m, int stock = 10, bool isActive = true)
    {
        var result = (Id: 0, Slug: "");
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            var builder = new ProductBuilder().WithName(name).WithPrice(price).WithStock(stock).InCategory(category);
            if (!isActive) builder = builder.Inactive();
            var product = builder.Build();
            db.Products.Add(product);
            await db.SaveChangesAsync(Ct);
            result = (product.Id, product.Slug);
        });
        return result;
    }

    // ═══════════════════════════════════════════════════════════
    // Oluşturma
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task Create_WithAdminToken_Returns201AndPersists()
    {
        await AuthenticateAsAdminAsync();
        var categoryId = await SeedCategoryAsync();

        var response = await Client.PostAsJsonAsync("/api/admin/products",
            new { name = "Yeni Kitap", price = 49.90m, stock = 5, categoryId }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<AdminProductDetailDto>(Ct);
        dto!.Slug.ShouldBe("yeni-kitap");
        dto.IsActive.ShouldBeTrue();

        var exists = await ExecuteDbAsync(db => db.Products.AnyAsync(p => p.Id == dto.Id, Ct));
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task Create_WithInvalidData_Returns400WithValidationDetails()
    {
        await AuthenticateAsAdminAsync();

        var response = await Client.PostAsJsonAsync("/api/admin/products",
            new { name = "", price = 0m, stock = 0, categoryId = 1 }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(Ct);
        body.ShouldContain("Name");
        body.ShouldContain("Price");
    }

    [Fact]
    public async Task Create_WithDuplicateSku_Returns409()
    {
        await AuthenticateAsAdminAsync();
        var categoryId = await SeedCategoryAsync();

        var first = await Client.PostAsJsonAsync("/api/admin/products",
            new { name = "Birinci Kitap", sku = "SKU-DUP", price = 50m, stock = 1, categoryId }, Ct);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        var second = await Client.PostAsJsonAsync("/api/admin/products",
            new { name = "İkinci Kitap", sku = "SKU-DUP", price = 60m, stock = 1, categoryId }, Ct);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_WhenSlugTakenBySoftDeletedProduct_GeneratesSuffix()
    {
        await AuthenticateAsAdminAsync();
        var categoryId = await SeedCategoryAsync();

        var first = await Client.PostAsJsonAsync("/api/admin/products",
            new { name = "Tekrarlanan İsim", price = 50m, stock = 1, categoryId }, Ct);
        var firstDto = await first.Content.ReadFromJsonAsync<AdminProductDetailDto>(Ct);

        (await Client.DeleteAsync($"/api/admin/products/{firstDto!.Id}", Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var second = await Client.PostAsJsonAsync("/api/admin/products",
            new { name = "Tekrarlanan İsim", price = 55m, stock = 1, categoryId }, Ct);

        second.StatusCode.ShouldBe(HttpStatusCode.Created);
        var secondDto = await second.Content.ReadFromJsonAsync<AdminProductDetailDto>(Ct);
        secondDto!.Slug.ShouldBe("tekrarlanan-isim-2");
    }

    [Fact]
    public async Task Create_WithPublisher_FillsPublisherNameAndBecomesSearchable()
    {
        await AuthenticateAsAdminAsync();
        var categoryId = await SeedCategoryAsync();
        var publisherId = await SeedPublisherAsync("Pupa Yayinlari");

        var response = await Client.PostAsJsonAsync("/api/admin/products",
            new { name = "Yayinevli Kitap", price = 50m, stock = 1, categoryId, publisherId }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var dto = await response.Content.ReadFromJsonAsync<AdminProductDetailDto>(Ct);
        dto!.PublisherName.ShouldBe("Pupa Yayinlari");

        var searchResponse = await Client.GetAsync(
            $"/api/search?q={Uri.EscapeDataString("Pupa Yayinlari")}", Ct);
        searchResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await searchResponse.Content.ReadFromJsonAsync<SearchResultDto>(Ct);
        result!.Results.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task Create_WithUnknownCategory_Returns400()
    {
        await AuthenticateAsAdminAsync();

        var response = await Client.PostAsJsonAsync("/api/admin/products",
            new { name = "Kategorisiz", price = 50m, stock = 1, categoryId = 999999 }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ═══════════════════════════════════════════════════════════
    // Güncelleme
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task Update_ThenPublicDetail_ReturnsFreshData()
    {
        var (id, slug) = await SeedProductViaDbAsync(price: 49.90m);

        var before = await Client.GetFromJsonAsync<ProductDetailDto>($"/api/products/{slug}", Ct);
        before!.Price.ShouldBe(49.90m);

        await AuthenticateAsAdminAsync();
        var categoryId = await ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == id).Select(p => p.CategoryId).FirstAsync(Ct));

        var updateResponse = await Client.PutAsJsonAsync($"/api/admin/products/{id}",
            new { name = "Güncellenmiş", price = 59.90m, categoryId, isActive = true }, Ct);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Cache invalidation olmasaydı 49.90 görürdük (PLAN.md'nin "en sinsi hata"sı).
        ClearAuthentication();
        var after = await Client.GetFromJsonAsync<ProductDetailDto>($"/api/products/{slug}", Ct);
        after!.Price.ShouldBe(59.90m);
    }

    [Fact]
    public async Task Update_DoesNotChangeSlug()
    {
        var (id, slug) = await SeedProductViaDbAsync();
        await AuthenticateAsAdminAsync();
        var categoryId = await ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == id).Select(p => p.CategoryId).FirstAsync(Ct));

        var response = await Client.PutAsJsonAsync($"/api/admin/products/{id}",
            new { name = "Tamamen Farklı İsim", price = 60m, categoryId, isActive = true }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var dbSlug = await ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == id).Select(p => p.Slug).FirstAsync(Ct));
        dbSlug.ShouldBe(slug);
    }

    // ═══════════════════════════════════════════════════════════
    // Soft delete / restore
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task Delete_SoftDeletes_AndDisappearsFromListing()
    {
        var (id, slug) = await SeedProductViaDbAsync();
        await AuthenticateAsAdminAsync();

        var response = await Client.DeleteAsync($"/api/admin/products/{id}", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var product = await ExecuteDbAsync(db =>
            db.Products.IgnoreQueryFilters().FirstAsync(p => p.Id == id, Ct));
        product.DeletedAt.ShouldNotBeNull();
        product.IsActive.ShouldBeFalse();

        ClearAuthentication();
        var publicResponse = await Client.GetAsync($"/api/products/{slug}", Ct);
        publicResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_Twice_Returns404()
    {
        var (id, _) = await SeedProductViaDbAsync();
        await AuthenticateAsAdminAsync();

        (await Client.DeleteAsync($"/api/admin/products/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var second = await Client.DeleteAsync($"/api/admin/products/{id}", Ct);

        second.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Restore_MakesProductVisibleAgain()
    {
        var (id, _) = await SeedProductViaDbAsync();
        await AuthenticateAsAdminAsync();
        (await Client.DeleteAsync($"/api/admin/products/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await Client.PostAsync($"/api/admin/products/{id}/restore", null, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var product = await ExecuteDbAsync(db => db.Products.FirstAsync(p => p.Id == id, Ct));
        product.DeletedAt.ShouldBeNull();
    }

    // ═══════════════════════════════════════════════════════════
    // Stok
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateStock_UpdatesValueAndInvalidatesCache()
    {
        var (id, slug) = await SeedProductViaDbAsync(stock: 3);
        var before = await Client.GetFromJsonAsync<ProductDetailDto>($"/api/products/{slug}", Ct);
        before!.Stock.ShouldBe(3);

        await AuthenticateAsAdminAsync();
        var response = await Client.PatchAsJsonAsync($"/api/admin/products/{id}/stock", new { stock = 42 }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        ClearAuthentication();
        var after = await Client.GetFromJsonAsync<ProductDetailDto>($"/api/products/{slug}", Ct);
        after!.Stock.ShouldBe(42);
    }

    [Fact]
    public async Task UpdateStock_WithStaleEntity_Returns409()
    {
        var (id, _) = await SeedProductViaDbAsync(stock: 10);

        // Task.WhenAll ile GERÇEK eşzamanlı iki HTTP isteği bu makinede ÖLÇÜLDÜ:
        // TestServer + yerel Postgres bu kadar ucuz bir sorguda güvenilir bir
        // şekilde çakışmıyor (iki istek de "200" dönebiliyor — kararsız test).
        // Bunun yerine xmin çakışmasını DETERMİNİSTİK kuruyoruz: bir DbContext
        // ürünü TAKİPLİ okur ("bayat" hâle gelecek), admin UCU (gerçek HTTP
        // isteği) araya girip stoğu değiştirir ve xmin'i ilerletir, sonra bayat
        // nesne kaydedilmeye çalışılır. DbUpdateConcurrencyException →409
        // çevirisi GlobalExceptionHandler'da zaten kanıtlı (CLAUDE.md); burada
        // asıl kanıtlanan, AdminProductService'in bu istisnayı YUTMADAN
        // dışarı verdiği (K9).
        using var staleScope = Factory.Services.CreateScope();
        var staleDb = staleScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var staleProduct = await staleDb.Products.FirstAsync(p => p.Id == id, Ct);

        await AuthenticateAsAdminAsync();
        var winner = await Client.PatchAsJsonAsync($"/api/admin/products/{id}/stock", new { stock = 20 }, Ct);
        winner.StatusCode.ShouldBe(HttpStatusCode.OK);

        staleProduct.Stock = 99;
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => staleDb.SaveChangesAsync(Ct));
    }

    // ═══════════════════════════════════════════════════════════
    // Toplu fiyat güncelleme
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task BulkPrice_WithUnknownId_ChangesNothing()
    {
        var (id, _) = await SeedProductViaDbAsync(price: 100m);
        await AuthenticateAsAdminAsync();

        var response = await Client.PostAsJsonAsync("/api/admin/products/bulk-price", new
        {
            items = new object[]
            {
                new { productId = id, price = 150m, discountedPrice = (decimal?)null },
                new { productId = 999999, price = 10m, discountedPrice = (decimal?)null }
            }
        }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var price = await ExecuteDbAsync(db =>
            db.Products.Where(p => p.Id == id).Select(p => p.Price).FirstAsync(Ct));
        price.ShouldBe(100m);
    }

    // ═══════════════════════════════════════════════════════════
    // Liste
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AdminList_ShowsInactiveAndDeletedProducts()
    {
        var (activeId, _) = await SeedProductViaDbAsync(name: "Aktif Ürün");
        var (inactiveId, _) = await SeedProductViaDbAsync(name: "Pasif Ürün", isActive: false);
        var (deletedId, _) = await SeedProductViaDbAsync(name: "Silinecek Ürün");

        await AuthenticateAsAdminAsync();
        (await Client.DeleteAsync($"/api/admin/products/{deletedId}", Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var result = await Client.GetFromJsonAsync<PagedResult<AdminProductListDto>>(
            "/api/admin/products?includeDeleted=true&pageSize=100", Ct);

        var ids = result!.Items.Select(p => p.Id).ToList();
        ids.ShouldContain(activeId);
        ids.ShouldContain(inactiveId);
        ids.ShouldContain(deletedId);
    }
}
