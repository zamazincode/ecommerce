using Commerce.Api.Common.Filters;

namespace Commerce.Api.Features.Admin.Categories;

public static class AdminCategoryEndpoints
{
    public static RouteGroupBuilder MapAdminCategoryEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/categories", GetCategories)
             .WithSummary("Tüm kategorileri listeler (pasifler dahil)")
             .Produces<IReadOnlyList<AdminCategoryDto>>();

        group.MapPost("/categories", CreateCategory)
             .WithValidation<CreateCategoryRequest>()
             .WithSummary("Yeni kategori oluşturur")
             .Produces<AdminCategoryDto>(StatusCodes.Status201Created)
             .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/categories/{id:int}", UpdateCategory)
             .WithValidation<UpdateCategoryRequest>()
             .WithSummary("Kategoriyi günceller (döngü koruması dahil)")
             .Produces<AdminCategoryDto>()
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IReadOnlyList<AdminCategoryDto>> GetCategories(
        AdminCategoryService service, CancellationToken ct)
        => await service.ListAsync(ct);

    private static async Task<IResult> CreateCategory(
        CreateCategoryRequest body, AdminCategoryService service, CancellationToken ct)
    {
        var category = await service.CreateAsync(body, ct);
        return TypedResults.Created($"/api/admin/categories/{category.Id}", category);
    }

    private static async Task<AdminCategoryDto> UpdateCategory(
        int id, UpdateCategoryRequest body, AdminCategoryService service, CancellationToken ct)
        => await service.UpdateAsync(id, body, ct);
}
