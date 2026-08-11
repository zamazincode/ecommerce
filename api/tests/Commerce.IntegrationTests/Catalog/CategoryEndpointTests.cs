using System.Net;
using System.Net.Http.Json;
using Commerce.Api.Common.Results;
using Commerce.Api.Features.Catalog.Dtos;
using Commerce.Api.Persistence;
using Commerce.Domain.Catalog;
using Commerce.IntegrationTests.Infrastructure;
using Shouldly;

namespace Commerce.IntegrationTests.Catalog;

public class CategoryEndpointTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// Kitap → Roman → Polisiye (üç seviyeli ağaç) + iki ürün
    private static async Task SeedTreeAsync(AppDbContext db)
    {
        var root = new Category { Name = "Kitap", Slug = "kitap", DisplayOrder = 0 };
        var roman = new Category { Name = "Roman", Slug = "roman", Parent = root, DisplayOrder = 0 };
        var polisiye = new Category { Name = "Polisiye", Slug = "polisiye", Parent = roman, DisplayOrder = 0 };
        db.Categories.AddRange(root, roman, polisiye);

        db.Products.AddRange(
            new ProductBuilder().WithName("Roman Kitabi").InCategory(roman).Build(),
            new ProductBuilder().WithName("Polisiye Kitabi").InCategory(polisiye).Build());

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetTree_ReturnsNestedStructure()
    {
        await ExecuteDbAsync(SeedTreeAsync);

        var tree = await Client.GetFromJsonAsync<List<CategoryTreeDto>>("/api/categories/tree", Ct);

        tree.ShouldNotBeNull();
        tree.Count.ShouldBe(1);                       // sadece kök
        tree[0].Name.ShouldBe("Kitap");
        tree[0].Children.Count.ShouldBe(1);
        tree[0].Children[0].Name.ShouldBe("Roman");
        tree[0].Children[0].Children[0].Name.ShouldBe("Polisiye");
    }

    [Fact]
    public async Task GetFlat_ReturnsAllCategories()
    {
        await ExecuteDbAsync(SeedTreeAsync);

        var flat = await Client.GetFromJsonAsync<List<CategoryDto>>("/api/categories", Ct);

        flat.ShouldNotBeNull();
        flat.Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetCategoryProducts_IncludesProductsFromDescendantCategories()
    {
        await ExecuteDbAsync(SeedTreeAsync);

        // "Kitap" kategorisini seçen kullanıcı, alt kategorilerdeki ürünleri de görmeli.
        var result = await Client.GetFromJsonAsync<PagedResult<ProductListDto>>(
            "/api/categories/kitap/products", Ct);

        result!.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetCategoryProducts_ForLeafCategory_ReturnsOnlyItsOwnProducts()
    {
        await ExecuteDbAsync(SeedTreeAsync);

        var result = await Client.GetFromJsonAsync<PagedResult<ProductListDto>>(
            "/api/categories/polisiye/products", Ct);

        result!.TotalCount.ShouldBe(1);
        result.Items[0].Name.ShouldBe("Polisiye Kitabi");
    }

    [Fact]
    public async Task GetCategoryProducts_WhenCategoryDoesNotExist_Returns404()
    {
        var response = await Client.GetAsync("/api/categories/olmayan-kategori/products", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetHome_ReturnsAllThreeBlocks()
    {
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            db.Products.AddRange(
                new ProductBuilder().WithName("Normal").WithPrice(100m).InCategory(category).Build(),
                new ProductBuilder().WithName("Indirimli").WithPrice(200m).WithDiscount(120m).InCategory(category).Build());
            await db.SaveChangesAsync();
        });

        var home = await Client.GetFromJsonAsync<HomeDto>("/api/home", Ct);

        home.ShouldNotBeNull();
        home.NewArrivals.Count.ShouldBe(2);
        // Sipariş yok → çok satanlar boş, yenilere düşüyor
        home.Bestsellers.Count.ShouldBe(2);
        home.Discounted.Count.ShouldBe(1);
        home.Discounted[0].Name.ShouldBe("Indirimli");
    }

    [Fact]
    public async Task GetPublishers_ReturnsSortedList()
    {
        await ExecuteDbAsync(async db =>
        {
            db.Publishers.AddRange(
                new Publisher { Name = "Zeta Yayinlari", Slug = "zeta-yayinlari" },
                new Publisher { Name = "Alfa Yayinlari", Slug = "alfa-yayinlari" });
            await db.SaveChangesAsync();
        });

        var publishers = await Client.GetFromJsonAsync<List<PublisherBriefDto>>(
            "/api/publishers", Ct);

        publishers!.Select(p => p.Name).ShouldBe(["Alfa Yayinlari", "Zeta Yayinlari"]);
    }

    [Fact]
    public async Task GetBrands_ReturnsSortedList()
    {
        await ExecuteDbAsync(async db =>
        {
            db.Brands.AddRange(
                new Brand { Name = "Victorinox", Slug = "victorinox" },
                new Brand { Name = "Anatolian", Slug = "anatolian" });
            await db.SaveChangesAsync();
        });

        var brands = await Client.GetFromJsonAsync<List<BrandBriefDto>>("/api/brands", Ct);

        brands!.Select(b => b.Name).ShouldBe(["Anatolian", "Victorinox"]);
    }

    [Fact]
    public async Task GetAuthorBySlug_WhenNotFound_Returns404()
    {
        var response = await Client.GetAsync("/api/authors/olmayan-yazar", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
