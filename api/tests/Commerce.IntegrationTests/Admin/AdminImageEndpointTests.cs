using System.Net;
using System.Net.Http.Json;
using Commerce.Api.Features.Admin.Images.Dtos;
using Commerce.Api.Features.Catalog.Dtos;
using Commerce.Api.Persistence.Identity;
using Commerce.Domain.Catalog;
using Commerce.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Commerce.IntegrationTests.Admin;

public class AdminImageEndpointTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(int Id, string Slug)> SeedProductAsync()
    {
        var result = (Id: 0, Slug: "");
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            var product = new ProductBuilder().WithName("Admin Görsel Testi").InCategory(category).Build();
            db.Products.Add(product);
            await db.SaveChangesAsync(Ct);
            result = (product.Id, product.Slug);
        });
        return result;
    }

    private Task<Guid> AuthenticateAsAdminAsync(string email = "admin-goersel@test.com")
        => AuthenticateAsync(email, role: AppRoles.Admin);

    // ═══════════════════════════════════════════════════════════
    // İmza ucu
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetSignature_WithoutToken_Returns401()
    {
        var response = await Client.PostAsync("/api/admin/images/signature", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSignature_WithCustomerToken_Returns403()
    {
        await AuthenticateAsync();

        var response = await Client.PostAsync("/api/admin/images/signature", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSignature_WithAdminToken_ReturnsFilledParams()
    {
        await AuthenticateAsAdminAsync();

        var response = await Client.PostAsync("/api/admin/images/signature", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<SignedUploadDto>(Ct);

        dto!.Url.ShouldNotBeNullOrWhiteSpace();
        dto.ApiKey.ShouldNotBeNullOrWhiteSpace();
        dto.Timestamp.ShouldNotBeNullOrWhiteSpace();
        dto.Signature.ShouldNotBeNullOrWhiteSpace();
        dto.Folder.ShouldBe("products");
    }

    [Fact]
    public async Task GetSignature_ResponseNeverContainsApiSecret()
    {
        await AuthenticateAsAdminAsync();

        var response = await Client.PostAsync("/api/admin/images/signature", null, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        body.ShouldNotContain("apiSecret", Case.Insensitive);
        body.ShouldNotContain("api_secret", Case.Insensitive);
        body.ShouldNotContain("\"secret\"", Case.Insensitive);
    }

    // ═══════════════════════════════════════════════════════════
    // Görsel listeleme
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task ListImages_ReturnsImagesInDisplayOrder()
    {
        await AuthenticateAsAdminAsync();
        var product = await SeedProductAsync();   // varsayılan görsel DisplayOrder = 0

        await Client.PostAsJsonAsync(
            $"/api/admin/products/{product.Id}/images",
            new { publicId = "products/ikinci-kapak" }, Ct);   // DisplayOrder = 1

        var images = await Client.GetFromJsonAsync<List<ProductImageDto>>(
            $"/api/admin/products/{product.Id}/images", Ct);

        images!.Count.ShouldBe(2);
        images.Select(i => i.DisplayOrder).ShouldBe([0, 1]);
        images[1].Url.ShouldStartWith("https://res.cloudinary.com/test-cloud");
    }

    [Fact]
    public async Task ListImages_ForUnknownProduct_ReturnsEmptyList()
    {
        await AuthenticateAsAdminAsync();

        var images = await Client.GetFromJsonAsync<List<ProductImageDto>>(
            "/api/admin/products/999999/images", Ct);

        images!.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListImages_WithoutToken_Returns401()
    {
        var product = await SeedProductAsync();

        var response = await Client.GetAsync($"/api/admin/products/{product.Id}/images", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListImages_WithCustomerToken_Returns403()
    {
        var product = await SeedProductAsync();
        await AuthenticateAsync();

        var response = await Client.GetAsync($"/api/admin/products/{product.Id}/images", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ═══════════════════════════════════════════════════════════
    // Görsel bağlama
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task AddImage_WithAdminToken_CreatesHostedRow()
    {
        await AuthenticateAsAdminAsync();
        var product = await SeedProductAsync();

        var response = await Client.PostAsJsonAsync(
            $"/api/admin/products/{product.Id}/images",
            new { publicId = "products/yeni-kapak", displayOrder = 0 }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var image = await ExecuteDbAsync(db => db.ProductImages
            .IgnoreQueryFilters()
            .SingleAsync(i => i.ProductId == product.Id && i.CloudinaryPublicId == "products/yeni-kapak", Ct));

        image.IsMigrated.ShouldBeTrue();
        image.SourceUrl.ShouldStartWith("https://res.cloudinary.com/test-cloud");
    }

    [Fact]
    public async Task AddImage_WithoutDisplayOrder_AppendsAfterExisting()
    {
        await AuthenticateAsAdminAsync();
        var product = await SeedProductAsync();   // varsayılan görsel DisplayOrder = 0

        var response = await Client.PostAsJsonAsync(
            $"/api/admin/products/{product.Id}/images",
            new { publicId = "products/ikinci-kapak" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<ProductImageDto>(Ct);

        dto!.DisplayOrder.ShouldBe(1);
    }

    [Fact]
    public async Task AddImage_WithInvalidPublicId_Returns400()
    {
        await AuthenticateAsAdminAsync();
        var product = await SeedProductAsync();

        var response = await Client.PostAsJsonAsync(
            $"/api/admin/products/{product.Id}/images",
            new { publicId = "products/../x" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddImage_ForUnknownProduct_Returns404()
    {
        await AuthenticateAsAdminAsync();

        var response = await Client.PostAsJsonAsync(
            "/api/admin/products/999999/images", new { publicId = "products/kapak" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddImage_WithCustomerToken_Returns403()
    {
        var product = await SeedProductAsync();
        await AuthenticateAsync();

        var response = await Client.PostAsJsonAsync(
            $"/api/admin/products/{product.Id}/images", new { publicId = "products/kapak" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddImage_InvalidatesProductDetailCache()
    {
        var product = await SeedProductAsync();

        // 1) Cache'i doldur.
        var before = await Client.GetFromJsonAsync<ProductDetailDto>($"/api/products/{product.Slug}", Ct);
        before!.ImageUrls.Count.ShouldBe(1);

        // 2) Admin görsel ekler.
        await AuthenticateAsAdminAsync();
        (await Client.PostAsJsonAsync(
            $"/api/admin/products/{product.Id}/images",
            new { publicId = "products/cache-testi" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        // 3) Aynı istek YENİ görseli döndürmeli — cache.RemoveByTagAsync
        // çağrılmadıysa bu 10 dakika eskisini görürdü (PLAN.md'nin "en sinsi hata"sı).
        ClearAuthentication();
        var after = await Client.GetFromJsonAsync<ProductDetailDto>($"/api/products/{product.Slug}", Ct);
        after!.ImageUrls.Count.ShouldBe(2);
    }

    // ═══════════════════════════════════════════════════════════
    // Görsel silme
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task DeleteImage_RemovesRowAndAsksStorageToDelete()
    {
        await AuthenticateAsAdminAsync();
        var product = await SeedProductAsync();

        var added = await Client.PostAsJsonAsync(
            $"/api/admin/products/{product.Id}/images", new { publicId = "products/silinecek" }, Ct);
        var image = await added.Content.ReadFromJsonAsync<ProductImageDto>(Ct);

        var response = await Client.DeleteAsync($"/api/admin/product-images/{image!.Id}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var exists = await ExecuteDbAsync(db => db.ProductImages
            .IgnoreQueryFilters().AnyAsync(i => i.Id == image.Id, Ct));
        exists.ShouldBeFalse();

        Factory.ImageStorage.DeletedPublicIds.ShouldContain("products/silinecek");
    }
}
