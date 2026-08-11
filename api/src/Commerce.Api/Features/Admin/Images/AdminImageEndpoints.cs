using Commerce.Api.Common.Filters;
using Commerce.Api.Features.Admin.Images.Dtos;
using Commerce.Api.Features.Auth;

namespace Commerce.Api.Features.Admin.Images;

public static class AdminImageEndpoints
{
    /// Faz 11 bu grubu (`/api/admin`) devralacak — imza ucu şimdiden burada
    /// duruyor, o fazda sadece dosyası taşınacak.
    public static IEndpointRouteBuilder MapAdminImageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
                       .WithTags("Admin")
                       .RequireAuthorization(AuthPolicies.AdminOnly);

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

        return app;
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
