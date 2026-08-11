using System.Text.RegularExpressions;

namespace Commerce.Api.Common.Images;

/// Admin, Cloudinary'ye DOĞRUDAN yüklüyor; dönen `public_id` istemci
/// kontrolündeki bir string ve hem `SourceUrl` kolonuna hem de kullanıcıya
/// gösterilen URL'e giriyor — doğrulanmadan kabul edilemez (plan K6).
public static partial class CloudinaryPublicId
{
    // "products/" öneki OPSİYONEL: Cloudinary'nin "dynamic folder" modundaki
    // hesaplarda public_id klasör önekini içermeyebilir (hesap olmadığı için
    // ölçülemedi — bu yüzden ikisi de kabul ediliyor, ama daha fazlası değil).
    // Ek '/', '.', boşluk, '?', '..' REDDEDİLİR → yol geçişi ve URL enjeksiyonu yok.
    [GeneratedRegex(@"^(products/)?[A-Za-z0-9][A-Za-z0-9_-]{0,190}$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public static bool IsValid(string? publicId)
        => !string.IsNullOrWhiteSpace(publicId) && Pattern().IsMatch(publicId);
}
