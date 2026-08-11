using Commerce.Api.Common.Filters;
using Commerce.Api.Features.Admin.Images.Dtos;

namespace Commerce.Api.Features.Admin.Images;

public static class AdminImageEndpoints
{
    /// Grup artık AdminEndpoints.MapAdminEndpoints'te kuruluyor (Faz 11/K1) —
    /// tek yetkilendirme noktası orada. Bu metot yalnızca kendi route'larını ekler.
    public static RouteGroupBuilder MapAdminImageEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/images/signature", GetSignature)
             .WithSummary("Cloudinary'ye doğrudan yükleme için imzalı parametreler")
             .Produces<SignedUploadDto>();

        group.MapPost("/products/{productId:int}/images", AddImage)
             .WithValidation<AddProductImageRequest>()
             .WithSummary("Yüklenmiş bir Cloudinary görselini ürüne bağlar")
             .Produces<ProductImageDto>(StatusCodes.Status201Created)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/product-images/{imageId:int}", DeleteImage)
             .WithSummary("Görseli üründen ve Cloudinary'den kaldırır")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static SignedUploadDto GetSignature(AdminImageService service)
        => service.GetSignature();

    private static async Task<IResult> AddImage(
        int productId, AddProductImageRequest body, AdminImageService service, CancellationToken ct)
    {
        var image = await service.AddAsync(productId, body, ct);
        return TypedResults.Created($"/api/admin/product-images/{image.Id}", image);
    }

    private static async Task<IResult> DeleteImage(
        int imageId, AdminImageService service, CancellationToken ct)
    {
        await service.DeleteAsync(imageId, ct);
        return TypedResults.NoContent();
    }
}
