namespace Commerce.Api.Persistence.Auditing;

/// Denetim kaydının kapsamını daraltan izin listesi (plan K2/S1).
/// "DbContext'ten geçen HER değişiklik" tasarımı ApplicationUser.PasswordHash,
/// RefreshToken.TokenHash, Payment.RawResponse gibi sırları denetim tablosuna
/// yazardı ve `import`/`seed` komutlarını binlerce satırla şişirirdi.
public static class AuditedEntities
{
    /// Denetlenen CLR tip adları. Listede OLMAYAN hiçbir entity kaydedilmez.
    public static readonly HashSet<string> Types =
        ["Product", "Category", "Coupon", "Review", "ProductImage", "Order"];

    /// Gölge/hesaplanan kolonlar: xmin sürüm damgası, tsvector/trigram arama
    /// kolonları. Serileştirilirlerse denetim satırı gereksiz yere şişer
    /// (NpgsqlTsVector System.Text.Json'da sessizce diziye çevriliyor — ölçüldü).
    public static readonly HashSet<string> SkippedProperties =
        ["xmin", "SearchVector", "SearchName"];
}
