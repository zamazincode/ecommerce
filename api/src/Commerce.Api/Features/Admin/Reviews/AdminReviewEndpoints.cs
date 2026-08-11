using Commerce.Api.Common.Results;

namespace Commerce.Api.Features.Admin.Reviews;

public static class AdminReviewEndpoints
{
    public static RouteGroupBuilder MapAdminReviewEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/reviews", GetReviews)
             .WithSummary("Yorumları listeler (onay bekleyenler filtrelenebilir)")
             .Produces<PagedResult<AdminReviewDto>>();

        group.MapPatch("/reviews/{id:int}/approve", ApproveReview)
             .WithSummary("Yorumu yayına alır")
             .Produces(StatusCodes.Status200OK)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/reviews/{id:int}", DeleteReview)
             .WithSummary("Yorumu kaldırır")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<PagedResult<AdminReviewDto>> GetReviews(
        [AsParameters] AdminReviewFilterRequest filter, AdminReviewService service, CancellationToken ct)
        => await service.SearchAsync(filter, ct);

    private static async Task<IResult> ApproveReview(
        int id, AdminReviewService service, CancellationToken ct)
    {
        await service.ApproveAsync(id, ct);
        return TypedResults.Ok();
    }

    private static async Task<IResult> DeleteReview(
        int id, AdminReviewService service, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return TypedResults.NoContent();
    }
}
