using System.Collections.Concurrent;
using System.Globalization;

namespace Commerce.Api.Common.Images;

/// Geliştirme ve test için. Gerçek bir Cloudinary çağrısı yapmaz.
/// `FakePaymentProvider` (Features/Payments) ile birebir aynı desen — bu sınıf
/// da `Commerce.Api` içinde yaşıyor, çünkü Cloudinary hesabı OLMAYAN bir
/// geliştirme ortamında da devreye girmesi gerekiyor (Faz 5/8 kararının aynısı).
public sealed class FakeImageStorage : IImageStorage
{
    public ConcurrentBag<string> DeletedPublicIds { get; } = [];
    public bool DeleteShouldFail { get; set; }

    public int DeleteCallCount => DeletedPublicIds.Count;

    public SignedUploadParams GetSignedUploadParams()
        => new(
            Url: "https://fake-upload.test/v1_1/fake-cloud/image/upload",
            ApiKey: "fake-api-key",
            Timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            Signature: "fake-signature",
            Folder: ProductImageUrls.Folder);

    public Task<bool> DeleteAsync(string publicId, CancellationToken ct = default)
    {
        DeletedPublicIds.Add(publicId);
        return Task.FromResult(!DeleteShouldFail);
    }
}
