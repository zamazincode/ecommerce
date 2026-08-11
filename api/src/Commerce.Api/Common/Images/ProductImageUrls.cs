using Commerce.Domain.Catalog;
using Microsoft.Extensions.Options;

namespace Commerce.Api.Common.Images;

/// Görselin hangi bağlamda gösterileceği — her biri farklı bir Cloudinary
/// dönüşüm önekine karşılık gelir (S3/S4).
public enum ImageTransformation { Card, Detail, Thumbnail, Original }

/// Cloudinary URL üretimi. Ağ çağrısı YAPMAZ, DI gerektirmez (yalnızca
/// ayarları okumak için IOptions alır) — bu yüzden EF sorgu projeksiyonlarında
/// doğrudan kullanılabilir (ölçüldü, plan 2.4).
///
/// DİKKAT: `Build`/`Resolve` metotları EF sorgusu İÇİNDE ÇAĞRILAMAZ — EF bir
/// metot çağrısını SQL'e çeviremez (`EffectivePrice` tuzağının aynı ailesi).
/// Sorgularda SADECE hazır önekler (`CardPrefix` vb.) `+` ile birleştirilir;
/// EF bunu `||` operatörüne çeviriyor. `Build`/`Resolve` yalnızca bellekte
/// materialize olmuş nesneler ve admin yazma yolu içindir.
public sealed class ProductImageUrls
{
    /// İstemciden alınmıyor (K6) — imzalanan `folder` ile birebir aynı olmalı,
    /// tek kaynak burası.
    public const string Folder = "products";

    /// Cloud adı yapılandırılmamışsa üretilen URL 404 verir — GÜRÜLTÜLÜ yanlış,
    /// sessiz yanlış değil ("demo" gibi gerçek bir cloud adına düşmek başkasının
    /// hesabına istek atmak olurdu).
    public const string FallbackCloudName = "cloudinary-yapilandirilmadi";

    public string CardPrefix { get; }
    public string DetailPrefix { get; }
    public string ThumbnailPrefix { get; }
    public string OriginalPrefix { get; }

    public ProductImageUrls(IOptions<CloudinarySettings> options)
    {
        var cloudName = string.IsNullOrWhiteSpace(options.Value.CloudName)
            ? FallbackCloudName
            : options.Value.CloudName;

        var basePrefix = $"https://res.cloudinary.com/{cloudName}/image/upload/";

        CardPrefix = basePrefix + "w_300,h_450,c_fill,f_auto,q_auto/";
        DetailPrefix = basePrefix + "w_600,h_900,c_fit,f_auto,q_auto/";
        ThumbnailPrefix = basePrefix + "w_80,h_120,c_fill,f_auto,q_auto/";
        OriginalPrefix = basePrefix;
    }

    public string Prefix(ImageTransformation transformation) => transformation switch
    {
        ImageTransformation.Card => CardPrefix,
        ImageTransformation.Detail => DetailPrefix,
        ImageTransformation.Thumbnail => ThumbnailPrefix,
        ImageTransformation.Original => OriginalPrefix,
        _ => throw new ArgumentOutOfRangeException(nameof(transformation))
    };

    public string Build(string publicId, ImageTransformation transformation)
        => Prefix(transformation) + publicId.TrimStart('/');

    public string Resolve(ProductImage image, ImageTransformation transformation)
        => image is { IsMigrated: true, CloudinaryPublicId: { } publicId }
            ? Build(publicId, transformation)
            : image.SourceUrl;
}
