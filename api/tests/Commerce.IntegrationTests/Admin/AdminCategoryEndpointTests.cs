using System.Net;
using System.Net.Http.Json;
using Commerce.Api.Features.Admin.Categories;
using Commerce.Api.Features.Catalog.Dtos;
using Commerce.Api.Persistence.Identity;
using Commerce.Domain.Catalog;
using Commerce.IntegrationTests.Infrastructure;
using Shouldly;

namespace Commerce.IntegrationTests.Admin;

public class AdminCategoryEndpointTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<Guid> AuthenticateAsAdminAsync(string email = "admin-kategori@test.com")
        => AuthenticateAsync(email, role: AppRoles.Admin);

    private async Task<int> SeedCategoryAsync(string name, int? parentId = null)
    {
        var id = 0;
        await ExecuteDbAsync(async db =>
        {
            var category = new Category
            {
                Name = name,
                Slug = SlugGeneratorForTest(name),
                ParentId = parentId
            };
            db.Categories.Add(category);
            await db.SaveChangesAsync(Ct);
            id = category.Id;
        });
        return id;
    }

    private static string SlugGeneratorForTest(string name)
        => Commerce.Domain.Common.SlugGenerator.Generate(name) + "-" + Guid.NewGuid().ToString("N")[..6];

    [Fact]
    public async Task Create_Returns201_AndAppearsInPublicTree()
    {
        await AuthenticateAsAdminAsync();

        var response = await Client.PostAsJsonAsync("/api/admin/categories", new { name = "Yeni Kategori" }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<AdminCategoryDto>(Ct);

        ClearAuthentication();
        var tree = await Client.GetFromJsonAsync<List<CategoryTreeDto>>("/api/categories/tree", Ct);
        tree!.ShouldContain(c => c.Id == dto!.Id);
    }

    [Fact]
    public async Task Create_WithUnknownParent_Returns400()
    {
        await AuthenticateAsAdminAsync();

        var response = await Client.PostAsJsonAsync("/api/admin/categories",
            new { name = "Yetim Kategori", parentId = 999999 }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_DuplicateName_GetsUniqueSlug()
    {
        await AuthenticateAsAdminAsync();

        var first = await Client.PostAsJsonAsync("/api/admin/categories", new { name = "Bilim Kurgu" }, Ct);
        var firstDto = await first.Content.ReadFromJsonAsync<AdminCategoryDto>(Ct);

        var second = await Client.PostAsJsonAsync("/api/admin/categories", new { name = "Bilim Kurgu" }, Ct);
        var secondDto = await second.Content.ReadFromJsonAsync<AdminCategoryDto>(Ct);

        secondDto!.Slug.ShouldNotBe(firstDto!.Slug);
        secondDto.Slug.ShouldBe($"{firstDto.Slug}-2");
    }

    [Fact]
    public async Task Update_RenamesAndInvalidatesTree()
    {
        var categoryId = await SeedCategoryAsync("Eski Ad");

        var before = await Client.GetFromJsonAsync<List<CategoryDto>>("/api/categories", Ct);
        before!.ShouldContain(c => c.Id == categoryId && c.Name == "Eski Ad");

        await AuthenticateAsAdminAsync();
        var response = await Client.PutAsJsonAsync($"/api/admin/categories/{categoryId}",
            new { name = "Yeni Ad", isActive = true }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        ClearAuthentication();
        var after = await Client.GetFromJsonAsync<List<CategoryDto>>("/api/categories", Ct);
        after!.ShouldContain(c => c.Id == categoryId && c.Name == "Yeni Ad");
    }

    [Fact]
    public async Task Update_WithSelfAsParent_Returns400()
    {
        var categoryId = await SeedCategoryAsync("Kendine Referans");
        await AuthenticateAsAdminAsync();

        var response = await Client.PutAsJsonAsync($"/api/admin/categories/{categoryId}",
            new { name = "Kendine Referans", parentId = categoryId, isActive = true }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_WithDescendantAsParent_Returns400()
    {
        var rootId = await SeedCategoryAsync("Kok");
        var childId = await SeedCategoryAsync("Cocuk", parentId: rootId);

        await AuthenticateAsAdminAsync();

        // Kök, kendi çocuğunun altına taşınamaz (K11) — kılavuzun bilerek
        // atladığı çok seviyeli döngü koruması.
        var response = await Client.PutAsJsonAsync($"/api/admin/categories/{rootId}",
            new { name = "Kok", parentId = childId, isActive = true }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
