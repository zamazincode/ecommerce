using Commerce.Api.Common.Results;

namespace Commerce.Api.Features.Admin.Products.Dtos;

public sealed record CreateProductRequest(
    string Name, string? Sku, string? Description,
    decimal Price, decimal? DiscountedPrice, int Stock,
    int CategoryId, int? PublisherId, int? BrandId, bool? IsActive);

public sealed record UpdateProductRequest(
    string Name, string? Description, decimal Price, decimal? DiscountedPrice,
    int CategoryId, int? PublisherId, int? BrandId, bool IsActive);

public sealed record UpdateStockRequest(int Stock);

public sealed record BulkPriceUpdateItem(int ProductId, decimal Price, decimal? DiscountedPrice);

public sealed record BulkPriceUpdateRequest(IReadOnlyList<BulkPriceUpdateItem> Items);

public sealed record BulkPriceUpdateResult(int Updated);

public sealed record AdminProductFilterRequest
{
    // TÜMÜ NULLABLE — [AsParameters] tuzağı (CLAUDE.md).
    public string? Q { get; init; }
    public int? CategoryId { get; init; }
    public bool? IsActive { get; init; }
    public bool? IncludeDeleted { get; init; }
    public string? SortBy { get; init; }
    public string? SortDir { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }

    public PageRequest ToPageRequest() => new()
    {
        Page = Page ?? 1,
        PageSize = PageSize ?? PageRequest.DefaultPageSize
    };
}

public sealed record AdminProductListDto(
    int Id, string Slug, string Name, string? Sku, decimal Price, decimal? DiscountedPrice,
    int Stock, bool IsActive, DateTime? DeletedAt, int CategoryId, string CategoryName,
    string? ThumbnailUrl, DateTime CreatedAt);

public sealed record AdminProductDetailDto(
    int Id, string Slug, string Name, string? Sku, string? Description,
    decimal Price, decimal? DiscountedPrice, int Stock, bool IsActive, DateTime? DeletedAt,
    int CategoryId, string CategoryName,
    int? PublisherId, string? PublisherName,
    int? BrandId, string? BrandName,
    DateTime CreatedAt, DateTime? UpdatedAt);
