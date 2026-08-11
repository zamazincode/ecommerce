using System.Net;
using System.Net.Http.Json;
using Commerce.Api.Common.Results;
using Commerce.Api.Features.Admin.Reviews;
using Commerce.Api.Persistence.Identity;
using Commerce.Domain.Reviews;
using Commerce.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Commerce.IntegrationTests.Admin;

/// Yorum OLUŞTURAN uç yok (PLAN.md Faz E3). Bu testler yalnızca moderasyon
/// altyapısının doğru çalıştığını doğruluyor — satırları elle DB'ye ekliyor.
public class AdminReviewEndpointTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<Guid> AuthenticateAsAdminAsync(string email = "admin-yorum@test.com")
        => AuthenticateAsync(email, role: AppRoles.Admin);

    private async Task<(int ReviewId, int ProductId)> SeedReviewAsync(bool isApproved = false)
    {
        var reviewerId = await CreateUserAsync("yorumcu@test.com", "Test1234", AppRoles.Customer);

        var result = (ReviewId: 0, ProductId: 0);
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            var product = new ProductBuilder().WithName("Yorum Testi Kitabı").InCategory(category).Build();
            db.Products.Add(product);
            await db.SaveChangesAsync(Ct);

            var review = new Review
            {
                ProductId = product.Id,
                UserId = reviewerId,
                Rating = 5,
                Comment = "Test yorumu",
                IsApproved = isApproved,
                CreatedAt = DateTime.UtcNow
            };
            db.Reviews.Add(review);
            await db.SaveChangesAsync(Ct);

            result = (review.Id, product.Id);
        });
        return result;
    }

    [Fact]
    public async Task List_ReturnsPendingReviews()
    {
        var (reviewId, _) = await SeedReviewAsync(isApproved: false);
        await AuthenticateAsAdminAsync();

        var result = await Client.GetFromJsonAsync<PagedResult<AdminReviewDto>>(
            "/api/admin/reviews?onlyPending=true", Ct);

        result!.Items.ShouldContain(r => r.Id == reviewId);
    }

    [Fact]
    public async Task Approve_SetsIsApprovedTrue()
    {
        var (reviewId, _) = await SeedReviewAsync(isApproved: false);
        await AuthenticateAsAdminAsync();

        var response = await Client.PatchAsync($"/api/admin/reviews/{reviewId}/approve", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var isApproved = await ExecuteDbAsync(db =>
            db.Reviews.Where(r => r.Id == reviewId).Select(r => r.IsApproved).FirstAsync(Ct));
        isApproved.ShouldBeTrue();
    }

    [Fact]
    public async Task Approve_UnknownId_Returns404()
    {
        await AuthenticateAsAdminAsync();

        var response = await Client.PatchAsync("/api/admin/reviews/999999/approve", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_RemovesReview()
    {
        var (reviewId, _) = await SeedReviewAsync();
        await AuthenticateAsAdminAsync();

        var response = await Client.DeleteAsync($"/api/admin/reviews/{reviewId}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var exists = await ExecuteDbAsync(db => db.Reviews.AnyAsync(r => r.Id == reviewId, Ct));
        exists.ShouldBeFalse();
    }
}
