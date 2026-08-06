namespace Commerce.Api.Features.Catalog.Dtos;

public sealed record CategoryDto(
    int Id,
    string Name,
    string Slug,
    int? ParentId,
    int DisplayOrder);

public sealed record CategoryTreeDto(
    int Id,
    string Name,
    string Slug,
    int DisplayOrder,
    IReadOnlyList<CategoryTreeDto> Children);

public sealed record AuthorDetailDto(int Id, string Name, string Slug, string? Bio);

public sealed record HomeDto(
    IReadOnlyList<ProductListDto> Bestsellers,
    IReadOnlyList<ProductListDto> NewArrivals,
    IReadOnlyList<ProductListDto> Discounted);
