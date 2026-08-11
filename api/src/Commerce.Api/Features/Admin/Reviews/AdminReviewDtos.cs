using Commerce.Api.Common.Results;

namespace Commerce.Api.Features.Admin.Reviews;

public sealed record AdminReviewFilterRequest
{
    public bool? OnlyPending { get; init; }
    public int? ProductId { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }

    public PageRequest ToPageRequest() => new()
    {
        Page = Page ?? 1,
        PageSize = PageSize ?? PageRequest.DefaultPageSize
    };
}

public sealed record AdminReviewDto(
    int Id, int ProductId, string ProductName, Guid UserId, string? UserEmail,
    int Rating, string? Comment, bool IsApproved, DateTime CreatedAt);
