namespace Commerce.Api.Features.Admin.Categories;

public sealed record CreateCategoryRequest(string Name, int? ParentId, int? DisplayOrder);

public sealed record UpdateCategoryRequest(string Name, int? ParentId, int? DisplayOrder, bool IsActive);

public sealed record AdminCategoryDto(
    int Id, string Name, string Slug, int? ParentId, int DisplayOrder, bool IsActive, int ProductCount);
