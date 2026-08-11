using Commerce.Api.Common.Filters;
using Commerce.Api.Common.Results;
using Commerce.Api.Features.Admin.Products.Dtos;

namespace Commerce.Api.Features.Admin.Products;

public static class AdminProductEndpoints
{
    public static RouteGroupBuilder MapAdminProductEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/products", GetProducts)
             .WithSummary("Ürünleri filtreli/sayfalı listeler (pasif/silinmiş dahil edilebilir)")
             .Produces<PagedResult<AdminProductListDto>>();

        group.MapGet("/products/{id:int}", GetProduct)
             .WithSummary("Ürün detayı (silinmiş ürün de görülebilir)")
             .Produces<AdminProductDetailDto>()
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/products", CreateProduct)
             .WithValidation<CreateProductRequest>()
             .WithSummary("Yeni ürün oluşturur")
             .Produces<AdminProductDetailDto>(StatusCodes.Status201Created)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/products/{id:int}", UpdateProduct)
             .WithValidation<UpdateProductRequest>()
             .WithSummary("Ürün bilgilerini günceller (slug sabit kalır)")
             .Produces<AdminProductDetailDto>()
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/products/{id:int}", DeleteProduct)
             .WithSummary("Ürünü soft-delete eder")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/products/{id:int}/restore", RestoreProduct)
             .WithSummary("Soft-delete edilmiş ürünü geri getirir (IsActive AÇILMAZ)")
             .Produces<AdminProductDetailDto>()
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/products/{id:int}/stock", UpdateStock)
             .WithValidation<UpdateStockRequest>()
             .WithSummary("Stok miktarını günceller")
             .Produces<AdminProductDetailDto>()
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/products/bulk-price", BulkUpdatePrice)
             .WithValidation<BulkPriceUpdateRequest>()
             .WithSummary("Birden çok ürünün fiyatını tek istekte günceller")
             .Produces<BulkPriceUpdateResult>()
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<PagedResult<AdminProductListDto>> GetProducts(
        [AsParameters] AdminProductFilterRequest filter, AdminProductService service, CancellationToken ct)
        => await service.SearchAsync(filter, ct);

    private static async Task<AdminProductDetailDto> GetProduct(
        int id, AdminProductService service, CancellationToken ct)
        => await service.GetAsync(id, ct);

    private static async Task<IResult> CreateProduct(
        CreateProductRequest body, AdminProductService service, CancellationToken ct)
    {
        var product = await service.CreateAsync(body, ct);
        return TypedResults.Created($"/api/admin/products/{product.Id}", product);
    }

    private static async Task<AdminProductDetailDto> UpdateProduct(
        int id, UpdateProductRequest body, AdminProductService service, CancellationToken ct)
        => await service.UpdateAsync(id, body, ct);

    private static async Task<IResult> DeleteProduct(
        int id, AdminProductService service, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<AdminProductDetailDto> RestoreProduct(
        int id, AdminProductService service, CancellationToken ct)
        => await service.RestoreAsync(id, ct);

    private static async Task<AdminProductDetailDto> UpdateStock(
        int id, UpdateStockRequest body, AdminProductService service, CancellationToken ct)
        => await service.UpdateStockAsync(id, body.Stock, ct);

    private static async Task<BulkPriceUpdateResult> BulkUpdatePrice(
        BulkPriceUpdateRequest body, AdminProductService service, CancellationToken ct)
        => await service.BulkUpdatePriceAsync(body.Items, ct);
}
