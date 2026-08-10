using System.Globalization;

namespace Commerce.Domain.Pricing;

/// Para birimi dönüşümleri YALNIZCA sağlayıcı sınırında yapılır.
/// Domain'in tamamı decimal TL ile çalışır; burası dış dünyaya çeviri katmanı.
public static class MoneyUnits
{
    /// Sağlayıcıdan gelen metni ayrıştırırken kullanılan stil. NumberStyles.Number
    /// KULLANILMAZ: binlik ayracını kabul eder ve InvariantCulture'da binlik ayracı
    /// VİRGÜLDÜR — "100,50" sessizce 10050 olur (Faz 3b'nin 100× hatasının aynısı).
    private const NumberStyles ProviderAmountStyles =
        NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign;

    /// Kuruşa çevirir. Bazı sağlayıcılar tutarı tam sayı "minor unit" bekler.
    /// 100.50 TL → 10050
    public static long ToMinorUnits(decimal amount)
        => (long)Math.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);

    /// 10050 → 100.50 TL
    public static decimal FromMinorUnits(long minorUnits)
        => minorUnits / 100m;

    /// Ondalık ayıracı NOKTA olan metin. iyzico bu biçimi bekliyor.
    ///
    /// CultureInfo.InvariantCulture ŞART. Sunucunun kültürü tr-TR ise
    /// amount.ToString("0.00") çağrısı "100,50" üretir — sağlayıcı bunu
    /// reddedebilir ya da daha kötüsü, farklı yorumlayabilir.
    public static string ToProviderString(decimal amount)
        => amount.ToString("0.00", CultureInfo.InvariantCulture);

    /// Sağlayıcıdan gelen metni okur.
    /// NumberStyles.Number YAZMA: binlik ayracını kabul eder, InvariantCulture'da
    /// binlik ayracı virgüldür — "209,7" sessizce 2097 olur. AllowDecimalPoint
    /// ile aynı girdi FormatException atar; hata gürültülü olur, sessiz kalmaz.
    public static decimal ParseProviderAmount(string? value)
        => decimal.TryParse(value, ProviderAmountStyles, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"Sağlayıcıdan gelen tutar okunamadı: '{value}'");
}
