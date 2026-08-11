namespace Commerce.Api.Common.Images;

/// Sunucu tarafında imzalanan yükleme parametreleri. `ApiSecret` ASLA
/// dışarı çıkmaz — istemci dosyayı bu parametrelerle DOĞRUDAN Cloudinary'ye
/// yükler, API sunucusu dosyayı hiç görmez.
public sealed record SignedUploadParams(
    string Url, string ApiKey, string Timestamp, string Signature, string Folder);

/// Yalnızca AĞ işlerini kapsar. URL üretimi (saf, ağ çağrısı yapmayan bir
/// işlem) BİLEREK burada değil — `ProductImageUrls`'te: bu arayüzün arkasına
/// koymak EF sorgu projeksiyonlarında kullanılamaz hâle getirirdi (plan K2/K3).
public interface IImageStorage
{
    /// Admin'in doğrudan Cloudinary'ye yükleme yapabilmesi için imzalı parametreler.
    SignedUploadParams GetSignedUploadParams();

    /// `public_id`'yi Cloudinary'den siler. "not found" da BAŞARI sayılır
    /// (idempotency: zaten silinmiş bir varlık için satırı temizlemeyi
    /// engellememeli).
    Task<bool> DeleteAsync(string publicId, CancellationToken ct = default);
}
