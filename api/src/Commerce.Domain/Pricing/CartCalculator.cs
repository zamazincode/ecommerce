using Commerce.Domain.Common;

namespace Commerce.Domain.Pricing;

/// Sepetteki tek bir satır. Veritabanından kopuk — bu yüzden test edilmesi kolay.
public sealed record CartLine(
    int ProductId,
    string Name,
    string Slug,
    decimal UnitPrice,
    int Quantity);

/// Kuponun hesaplamaya giren kısmı. Geçerlilik kontrolü CouponValidator'ın işi.
public sealed record CouponInfo(
    string Code,
    CouponType Type,
    decimal Value,
    decimal MinCartTotal);

public sealed record CartTotals(
    decimal SubTotal,
    decimal DiscountAmount,
    decimal ShippingCost,
    decimal Total);

/// SAF SINIF. Veritabanına gitmez, saat okumaz, log yazmaz.
/// Aynı girdi her zaman aynı çıktıyı verir — test etmek 3 satır.
///
/// Faz 7'de sipariş oluştururken de BU sınıf kullanılacak.
/// Sepette gördüğü tutarla ödediği tutarın aynı olmasının garantisi bu.
public static class CartCalculator
{
    public static CartTotals Calculate(
        IReadOnlyList<CartLine> lines, CouponInfo? coupon = null)
    {
        if (lines.Count == 0)
            return new CartTotals(0m, 0m, 0m, 0m);

        var subTotal = CalculateSubTotal(lines);
        var discount = CalculateDiscount(subTotal, coupon);

        // KARAR: Kargo eşiği indirim SONRASI tutara bakar.
        // Alternatifi (indirim öncesine bakmak) müşteri lehine ama kupon +
        // bedava kargo birleşince marjı iki kez veriyorsun.
        var discountedSubTotal = subTotal - discount;
        var shipping = ShippingCalculator.Calculate(discountedSubTotal);

        var total = Round(discountedSubTotal + shipping);

        return new CartTotals(subTotal, discount, shipping, total);
    }

    /// Kupon doğrulaması ile indirim hesabının AYNI sayıya bakması için
    /// ara toplam tek yerden üretilir (yuvarlama farkı sınır değerde
    /// "kupon geçerli mi" sorusuna iki farklı cevap veriyordu).
    public static decimal CalculateSubTotal(IReadOnlyList<CartLine> lines)
        => Round(lines.Sum(l => l.UnitPrice * l.Quantity));

    private static decimal CalculateDiscount(decimal subTotal, CouponInfo? coupon)
    {
        if (coupon is null) return 0m;

        // Minimum sepet tutarı sağlanmıyorsa kupon uygulanmaz.
        if (subTotal < coupon.MinCartTotal) return 0m;

        var raw = coupon.Type switch
        {
            CouponType.Percentage => subTotal * coupon.Value / 100m,
            CouponType.FixedAmount => coupon.Value,
            _ => 0m
        };

        // İndirim ara toplamı ASLA aşamaz. Aksi hâlde 50₺'lik sepete
        // 100₺ kupon uygulayınca toplam -50₺ olur ve müşteriye para ödersin.
        // Alt sınır (0m) de gerekli: bozuk/negatif bir kupon değeri toplamı artırmasın.
        return Round(Math.Clamp(raw, 0m, subTotal));
    }

    /// Para her zaman 2 basamak. MidpointRounding.AwayFromZero:
    /// .NET'in varsayılanı "bankacı yuvarlaması"dır (2.5 → 2), ticarette
    /// beklenen davranış değildir (2.5 → 3).
    private static decimal Round(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
