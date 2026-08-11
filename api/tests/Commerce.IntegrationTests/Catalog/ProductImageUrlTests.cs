using System.Net.Http.Json;
using Commerce.Api.Common.Results;
using Commerce.Api.Features.Cart.Dtos;
using Commerce.Api.Features.Catalog.Dtos;
using Commerce.Api.Features.Search;
using Commerce.IntegrationTests.Infrastructure;
using Shouldly;

namespace Commerce.IntegrationTests.Catalog;

/// Görsel projeksiyonunun ALTI sorgu yerinin (ListProjection paylaşan beşi +
/// LoadDetailAsync) her birinin gerçekten güncellendiğini kanıtlıyor. Biri
/// unutulursa ilgili test "example.test"/i.dr.com.tr görür ve kırmızı yanar
/// (plan §7.4).
public class ProductImageUrlTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// Biri Cloudinary'de barınan, biri D&R kaynaklı iki ürün — aynı yanıtta
    /// karşılaştırılabilsinler diye ortak adlarla.
    private async Task<(string HostedSlug, string SourceSlug, int HostedProductId)>
        SeedComparableProductsAsync(string sharedNameFragment = "Karsilastirma")
    {
        var hostedId = 0;
        var hostedSlug = "";
        var sourceSlug = "";

        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);

            var hosted = new ProductBuilder()
                .WithName($"{sharedNameFragment} Cloudinary Kitabı")
                .InCategory(category)
                .WithCloudinaryImage("products/kapak1")
                .Build();

            var sourced = new ProductBuilder()
                .WithName($"{sharedNameFragment} Kaynak Kitabı")
                .InCategory(category)
                .Build();

            db.Products.AddRange(hosted, sourced);
            await db.SaveChangesAsync(Ct);

            hostedId = hosted.Id;
            hostedSlug = hosted.Slug;
            sourceSlug = sourced.Slug;
        });

        return (hostedSlug, sourceSlug, hostedId);
    }

    [Fact]
    public async Task ProductList_ReturnsCloudinaryUrlForHostedAndSourceUrlForRest()
    {
        var (hostedSlug, sourceSlug, _) = await SeedComparableProductsAsync();

        var result = await Client.GetFromJsonAsync<PagedResult<ProductListDto>>(
            "/api/products?pageSize=50", Ct);

        var hosted = result!.Items.Single(p => p.Slug == hostedSlug);
        var source = result.Items.Single(p => p.Slug == sourceSlug);

        hosted.ImageUrl.ShouldBe(
            "https://res.cloudinary.com/test-cloud/image/upload/w_300,h_450,c_fill,f_auto,q_auto/products/kapak1");
        source.ImageUrl.ShouldBe("https://example.test/kapak.jpg");
    }

    [Fact]
    public async Task ProductDetail_UsesDetailTransformation()
    {
        var (hostedSlug, _, _) = await SeedComparableProductsAsync();

        var detail = await Client.GetFromJsonAsync<ProductDetailDto>($"/api/products/{hostedSlug}", Ct);

        detail!.ImageUrls.Single().ShouldBe(
            "https://res.cloudinary.com/test-cloud/image/upload/w_600,h_900,c_fit,f_auto,q_auto/products/kapak1");
    }

    [Fact]
    public async Task Search_ReturnsCloudinaryUrl()
    {
        var (hostedSlug, _, _) = await SeedComparableProductsAsync("Aramaozel");

        var result = await Client.GetFromJsonAsync<SearchResultDto>("/api/search?q=Aramaozel", Ct);

        var hit = result!.Results.Items.Single(p => p.Slug == hostedSlug);
        hit.ImageUrl.ShouldBe(
            "https://res.cloudinary.com/test-cloud/image/upload/w_300,h_450,c_fill,f_auto,q_auto/products/kapak1");
    }

    [Fact]
    public async Task Suggest_UsesThumbnailTransformation()
    {
        var (hostedSlug, _, _) = await SeedComparableProductsAsync("Oneriozel");

        var suggestions = await Client.GetFromJsonAsync<List<SuggestionDto>>(
            "/api/search/suggest?q=Oneriozel", Ct);

        var hit = suggestions!.Single(s => s.Slug == hostedSlug);
        hit.ImageUrl.ShouldBe(
            "https://res.cloudinary.com/test-cloud/image/upload/w_80,h_120,c_fill,f_auto,q_auto/products/kapak1");
    }

    [Fact]
    public async Task Cart_UsesThumbnailTransformation()
    {
        await AuthenticateAsync();
        var (_, _, hostedProductId) = await SeedComparableProductsAsync();

        (await Client.PostAsJsonAsync("/api/cart/items", new { productId = hostedProductId, quantity = 1 }, Ct))
            .EnsureSuccessStatusCode();

        var cart = await Client.GetFromJsonAsync<CartDto>("/api/cart", Ct);

        cart!.Items.Single().ImageUrl.ShouldBe(
            "https://res.cloudinary.com/test-cloud/image/upload/w_80,h_120,c_fill,f_auto,q_auto/products/kapak1");
    }

    [Fact]
    public async Task Home_ReturnsCloudinaryUrl()
    {
        var (hostedSlug, _, _) = await SeedComparableProductsAsync();

        var home = await Client.GetFromJsonAsync<HomeDto>("/api/home", Ct);

        var hit = home!.NewArrivals.Single(p => p.Slug == hostedSlug);
        hit.ImageUrl.ShouldBe(
            "https://res.cloudinary.com/test-cloud/image/upload/w_300,h_450,c_fill,f_auto,q_auto/products/kapak1");
    }
}
