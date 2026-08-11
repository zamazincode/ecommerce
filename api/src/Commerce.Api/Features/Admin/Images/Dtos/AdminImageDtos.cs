namespace Commerce.Api.Features.Admin.Images.Dtos;

/// İstemcinin Cloudinary'ye DOĞRUDAN yükleme yapabilmesi için imzalı
/// parametreler. `ApiSecret` YOK — yalnızca public `ApiKey` ve imza.
public sealed record SignedUploadDto(
    string Url, string ApiKey, string Timestamp, string Signature, string Folder);

/// İstemci Cloudinary'ye yükledikten SONRA dönen `public_id`'yi ürüne bağlamak
/// için gönderilir. Klasör istemciden alınmaz (K6) — sunucuda sabit.
public sealed record AddProductImageRequest(string PublicId, int? DisplayOrder);

public sealed record ProductImageDto(
    int Id, int ProductId, string Url, int DisplayOrder, bool Hosted);
