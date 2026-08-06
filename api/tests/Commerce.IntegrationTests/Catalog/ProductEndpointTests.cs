using System.Net;
using System.Net.Http.Json;
using Commerce.Api.Common.Results;
using Commerce.Api.Features.Catalog.Dtos;
using Commerce.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Commerce.IntegrationTests.Catalog;

public class ProductEndpointTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<PagedResult<ProductListDto>> GetPageAsync(string url)
    {
        var response = await Client.GetAsync(url, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<ProductListDto>>(Ct);
        result.ShouldNotBeNull();
        return result;
    }

    [Fact]
    public async Task GetProducts_ReturnsRequestedPageSize()
    {
        // Arrange
        await ExecuteDbAsync(db => CatalogTestData.SeedCategoryWithProductsAsync(db, 25));

        // Act
        var result = await GetPageAsync("/api/products?pageSize=10");

        // Assert
        result.Items.Count.ShouldBe(10);
        result.TotalCount.ShouldBe(25);
        result.TotalPages.ShouldBe(3);
        result.HasNext.ShouldBeTrue();
        result.HasPrevious.ShouldBeFalse();
    }

    [Fact]
    public async Task GetProducts_SecondPage_ReturnsDifferentItems()
    {
        await ExecuteDbAsync(db => CatalogTestData.SeedCategoryWithProductsAsync(db, 25));

        var page1 = await GetPageAsync("/api/products?page=1&pageSize=10");
        var page2 = await GetPageAsync("/api/products?page=2&pageSize=10");

        var ids1 = page1.Items.Select(p => p.Id).ToHashSet();
        var ids2 = page2.Items.Select(p => p.Id).ToHashSet();

        // Kesişim boş olmalı. Bu test, ikincil sıralama (ThenBy(p => p.Id))
        // olmadığında ARA SIRA patlar — sinsi hatayı yakalayan test budur.
        ids1.Intersect(ids2).ShouldBeEmpty();
    }

    [Fact]
    public async Task GetProducts_WithExcessivePageSize_ClampsToMaximum()
    {
        await ExecuteDbAsync(db => CatalogTestData.SeedCategoryWithProductsAsync(db, 5));

        var result = await GetPageAsync("/api/products?pageSize=999999");

        result.PageSize.ShouldBe(PageRequest.MaxPageSize);
    }

    [Fact]
    public async Task GetProducts_FilteredByCategory_ReturnsOnlyThatCategory()
    {
        // Arrange — iki ayrı kategori
        var targetCategoryId = 0;
        await ExecuteDbAsync(async db =>
        {
            var target = await CatalogTestData.SeedCategoryWithProductsAsync(db, 3);
            await CatalogTestData.SeedCategoryWithProductsAsync(db, 4);
            targetCategoryId = target.Id;
        });

        // Act
        var result = await GetPageAsync($"/api/products?categoryId={targetCategoryId}");

        // Assert
        result.TotalCount.ShouldBe(3);
        result.Items.ShouldAllBe(p => p.CategoryId == targetCategoryId);
    }

    [Fact]
    public async Task GetProducts_FilteredByPriceRange_UsesEffectivePrice()
    {
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            db.Products.AddRange(
                new ProductBuilder().WithName("Ucuz").WithPrice(30m).InCategory(category).Build(),
                new ProductBuilder().WithName("Orta").WithPrice(75m).InCategory(category).Build(),
                new ProductBuilder().WithName("Pahali").WithPrice(500m).InCategory(category).Build(),
                // 400₺ ama indirimli 80₺ → 50-100 aralığına GİRMELİ
                new ProductBuilder().WithName("Indirimli").WithPrice(400m).WithDiscount(80m).InCategory(category).Build());
            await db.SaveChangesAsync();
        });

        var result = await GetPageAsync("/api/products?minPrice=50&maxPrice=100");

        result.TotalCount.ShouldBe(2);
        result.Items.Select(p => p.Name).OrderBy(n => n).ShouldBe(["Indirimli", "Orta"]);
        result.Items.ShouldAllBe(p => p.EffectivePrice >= 50m && p.EffectivePrice <= 100m);
    }

    [Fact]
    public async Task GetProducts_SortedByPriceAsc_ReturnsAscendingOrder()
    {
        await ExecuteDbAsync(db => CatalogTestData.SeedCategoryWithProductsAsync(db, 10));

        var result = await GetPageAsync("/api/products?sortBy=price&sortDir=asc&pageSize=10");

        var prices = result.Items.Select(p => p.EffectivePrice).ToList();
        prices.ShouldBe(prices.OrderBy(p => p).ToList());
    }

    [Fact]
    public async Task GetProducts_WithMaliciousSortField_ReturnsOkAndDoesNotCrash()
    {
        await ExecuteDbAsync(db => CatalogTestData.SeedCategoryWithProductsAsync(db, 3));

        var response = await Client.GetAsync("/api/products?sortBy=DROP%20TABLE%20Products", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Tablo hâlâ duruyor mu?
        var stillThere = await ExecuteDbAsync(db => db.Products.CountAsync(Ct));
        stillThere.ShouldBe(3);
    }

    [Fact]
    public async Task GetProducts_WithInStockFilter_ExcludesOutOfStock()
    {
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            db.Products.AddRange(
                new ProductBuilder().WithName("Stokta").WithStock(5).InCategory(category).Build(),
                new ProductBuilder().WithName("Tukendi").WithStock(0).InCategory(category).Build());
            await db.SaveChangesAsync();
        });

        var result = await GetPageAsync("/api/products?inStock=true");

        result.TotalCount.ShouldBe(1);
        result.Items[0].Name.ShouldBe("Stokta");
    }

    [Fact]
    public async Task GetProducts_DoesNotReturnInactiveProducts()
    {
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            db.Products.AddRange(
                new ProductBuilder().WithName("Yayinda").InCategory(category).Build(),
                new ProductBuilder().WithName("Pasif").Inactive().InCategory(category).Build());
            await db.SaveChangesAsync();
        });

        var result = await GetPageAsync("/api/products");

        result.TotalCount.ShouldBe(1);
        result.Items[0].Name.ShouldBe("Yayinda");
    }

    [Fact]
    public async Task GetProductBySlug_WhenExists_ReturnsDetailWithFreshStock()
    {
        var slug = "";
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            var product = new ProductBuilder()
                .WithName("Suç ve Ceza").WithPrice(180m).WithStock(7).InCategory(category).Build();
            db.Products.Add(product);
            await db.SaveChangesAsync();
            slug = product.Slug;
        });

        var response = await Client.GetAsync($"/api/products/{slug}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<ProductDetailDto>(Ct);
        detail!.Name.ShouldBe("Suç ve Ceza");
        detail.Stock.ShouldBe(7);
        detail.InStock.ShouldBeTrue();
        detail.Category.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetProductBySlug_ReturnsFreshStockEvenWhenDetailIsCached()
    {
        // Ürün detayı 10 dk cache'leniyor ama STOK cache'lenmiyor.
        // "Son 1 adet" yazan sayfa 10 dakika eski olamaz.
        var slug = "";
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            var product = new ProductBuilder()
                .WithName("Stok Testi").WithStock(10).InCategory(category).Build();
            db.Products.Add(product);
            await db.SaveChangesAsync();
            slug = product.Slug;
        });

        // İlk istek — detay cache'e giriyor
        var first = await Client.GetFromJsonAsync<ProductDetailDto>($"/api/products/{slug}", Ct);
        first!.Stock.ShouldBe(10);

        // Stok değişti
        await ExecuteDbAsync(async db =>
            await db.Products.Where(p => p.Slug == slug)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, 2), Ct));

        // İkinci istek — detay cache'ten geliyor ama stok TAZE olmalı
        var second = await Client.GetFromJsonAsync<ProductDetailDto>($"/api/products/{slug}", Ct);
        second!.Stock.ShouldBe(2);
        second.Name.ShouldBe("Stok Testi");
    }

    [Fact]
    public async Task GetProductBySlug_WhenNotFound_Returns404WithProblemDetails()
    {
        var response = await Client.GetAsync("/api/products/boyle-bir-urun-yok", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var body = await response.Content.ReadAsStringAsync(Ct);
        body.ShouldContain("traceId");
    }

    [Fact]
    public async Task GetRelated_ReturnsProductsFromSameCategoryExcludingItself()
    {
        var productId = 0;
        await ExecuteDbAsync(async db =>
        {
            var category = await CatalogTestData.SeedCategoryWithProductsAsync(db, 5);
            productId = await db.Products.Where(p => p.CategoryId == category.Id)
                .Select(p => p.Id).FirstAsync(Ct);
        });

        var related = await Client.GetFromJsonAsync<List<ProductListDto>>(
            $"/api/products/{productId}/related", Ct);

        related.ShouldNotBeNull();
        related.Count.ShouldBe(4);
        related.ShouldNotContain(p => p.Id == productId);
    }

    [Fact]
    public async Task GetRelated_WithUnknownProduct_Returns404()
    {
        var response = await Client.GetAsync("/api/products/999999/related", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
