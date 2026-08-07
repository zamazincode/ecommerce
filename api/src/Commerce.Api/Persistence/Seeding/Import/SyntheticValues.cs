namespace Commerce.Api.Persistence.Seeding.Import;

/// Kaynakta olmayan ama katalogun çalışması için gereken iki alan:
/// stok adedi ve ürünün eklenme tarihi. D&R çıktısında stok yalnızca
/// "InStock" bilgisi olarak var (hepsi aynı), çekilme zamanı da tek bir an.
///
/// Sabit bir sayı yazmak yerine SKU'dan türetiyoruz:
///   - "stokta yok" senaryosu test edilebilir kalır,
///   - "yeniler önce" sıralaması anlamlı çalışır,
///   - üretim deterministik: aynı SKU her aktarımda aynı değeri alır.
public static class SyntheticValues
{
    /// FNV-1a 32-bit. string.GetHashCode KULLANILMAZ: .NET'te süreç başına
    /// rastgeleleştirilir, iki çalıştırmada farklı sonuç verir.
    public static uint StableHash(string value)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;
        foreach (var ch in value)
        {
            hash ^= ch;
            hash *= prime;
        }

        return hash;
    }

    /// Ürünlerin ~%8'i stoksuz, kalanı 1–120 arası.
    public static int StockFor(string sku)
    {
        var hash = StableHash(sku);
        if (hash % 100 < 8) return 0;

        return (int)(hash / 100 % 120) + 1;
    }

    /// Son 2 yıla yayılmış UTC damga.
    public static DateTime CreatedAtFor(string sku, DateTime nowUtc)
    {
        var hash = StableHash(sku);
        var days = (int)(hash % 730);
        var minutes = (int)(hash / 730 % 1440);

        return DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc)
            .AddDays(-days)
            .AddMinutes(-minutes);
    }
}
