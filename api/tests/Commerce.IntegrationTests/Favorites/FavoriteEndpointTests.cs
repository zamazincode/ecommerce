using System.Net;
using System.Net.Http.Json;
using Commerce.Api.Features.Catalog.Dtos;
using Commerce.IntegrationTests.Infrastructure;
using Shouldly;

namespace Commerce.IntegrationTests.Favorites;

public class FavoriteEndpointTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<int> SeedProductAsync()
    {
        var productId = 0;
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            var product = new ProductBuilder().WithName("Favori Testi").InCategory(category).Build();
            db.Products.Add(product);
            await db.SaveChangesAsync(Ct);
            productId = product.Id;
        });
        return productId;
    }

    [Fact]
    public async Task AddThenList_ReturnsProductInFavorites()
    {
        var productId = await SeedProductAsync();
        await AuthenticateAsync();

        var addResponse = await Client.PostAsync($"/api/favorites/{productId}", null, Ct);
        addResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var favorites = await Client.GetFromJsonAsync<List<ProductListDto>>("/api/favorites", Ct);
        favorites!.Select(f => f.Id).ShouldContain(productId);
    }

    [Fact]
    public async Task AddTwice_IsIdempotent()
    {
        var productId = await SeedProductAsync();
        await AuthenticateAsync();

        await Client.PostAsync($"/api/favorites/{productId}", null, Ct);
        var second = await Client.PostAsync($"/api/favorites/{productId}", null, Ct);

        second.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var favorites = await Client.GetFromJsonAsync<List<ProductListDto>>("/api/favorites", Ct);
        favorites!.Count(f => f.Id == productId).ShouldBe(1); // TEK satır, iki değil
    }

    [Fact]
    public async Task Add_WithUnknownProduct_Returns404()
    {
        await AuthenticateAsync();

        var response = await Client.PostAsync("/api/favorites/999999", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetIds_ReturnsOnlyFavoritedProductIds()
    {
        var favoritedId = await SeedProductAsync();
        var otherId = await SeedProductAsync();
        await AuthenticateAsync();

        await Client.PostAsync($"/api/favorites/{favoritedId}", null, Ct);

        var ids = await Client.GetFromJsonAsync<List<int>>("/api/favorites/ids", Ct);
        ids!.ShouldContain(favoritedId);
        ids!.ShouldNotContain(otherId);
    }

    [Fact]
    public async Task Remove_DeletesFavorite()
    {
        var productId = await SeedProductAsync();
        await AuthenticateAsync();
        await Client.PostAsync($"/api/favorites/{productId}", null, Ct);

        var deleteResponse = await Client.DeleteAsync($"/api/favorites/{productId}", Ct);

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var favorites = await Client.GetFromJsonAsync<List<ProductListDto>>("/api/favorites", Ct);
        favorites!.ShouldBeEmpty();
    }

    [Fact]
    public async Task Remove_WhenNeverFavorited_ReturnsNoContentSilently()
    {
        await AuthenticateAsync();

        var response = await Client.DeleteAsync("/api/favorites/999999", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetAll_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/favorites", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Favorites_AreIsolatedBetweenUsers()
    {
        var productId = await SeedProductAsync();
        await AuthenticateAsync("kullanici-a@test.com");
        await Client.PostAsync($"/api/favorites/{productId}", null, Ct);

        var otherUserClient = await CreateAuthenticatedClientAsync("kullanici-b@test.com");
        var favorites = await otherUserClient.GetFromJsonAsync<List<ProductListDto>>("/api/favorites", Ct);

        favorites!.ShouldBeEmpty();
    }
}
