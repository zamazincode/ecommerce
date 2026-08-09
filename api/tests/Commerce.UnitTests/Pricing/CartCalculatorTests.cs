using Commerce.Domain.Common;
using Commerce.Domain.Pricing;
using Shouldly;

namespace Commerce.UnitTests.Pricing;

public class CartCalculatorTests
{
    private static CartLine Line(decimal unitPrice, int quantity = 1, int id = 1)
        => new(id, $"Ürün {id}", $"urun-{id}", unitPrice, quantity);

    // ── Ara toplam ───────────────────────────────────────────

    [Fact]
    public void Calculate_SumsLineTotals()
    {
        var lines = new[] { Line(100m, 2), Line(50m, 3, id: 2) };

        var result = CartCalculator.Calculate(lines);

        result.SubTotal.ShouldBe(350m);   // 200 + 150
    }

    [Fact]
    public void Calculate_WithEmptyCart_ReturnsAllZeros()
    {
        var result = CartCalculator.Calculate([]);

        result.SubTotal.ShouldBe(0m);
        result.DiscountAmount.ShouldBe(0m);
        result.ShippingCost.ShouldBe(0m);
        result.Total.ShouldBe(0m);
    }

    // ── Kargo ────────────────────────────────────────────────

    [Fact]
    public void Calculate_WhenBelowFreeShippingThreshold_AddsShippingCost()
    {
        var result = CartCalculator.Calculate([Line(150m)]);

        result.ShippingCost.ShouldBe(29.90m);
        result.Total.ShouldBe(179.90m);
    }

    [Fact]
    public void Calculate_WhenAtOrAboveThreshold_ShippingIsFree()
    {
        var result = CartCalculator.Calculate([Line(200m)]);

        result.ShippingCost.ShouldBe(0m);
        result.Total.ShouldBe(200m);
    }

    [Fact]
    public void Calculate_ShippingThresholdUsesDiscountedSubTotal()
    {
        // 220₺ sepet, %20 indirim → 176₺. Eşiğin ALTINA düşüyor, kargo ücretli.
        // Bu, projedeki bilinçli kararlardan biri; testi bunu sabitliyor.
        var coupon = new CouponInfo("YIRMI", CouponType.Percentage, 20m, MinCartTotal: 0m);

        var result = CartCalculator.Calculate([Line(220m)], coupon);

        result.SubTotal.ShouldBe(220m);
        result.DiscountAmount.ShouldBe(44m);
        result.ShippingCost.ShouldBe(29.90m);
        result.Total.ShouldBe(205.90m);    // 220 - 44 + 29.90
    }

    // ── Kupon ────────────────────────────────────────────────

    [Fact]
    public void Calculate_WithPercentageCoupon_AppliesCorrectDiscount()
    {
        var coupon = new CouponInfo("ON", CouponType.Percentage, 10m, MinCartTotal: 100m);

        var result = CartCalculator.Calculate([Line(300m)], coupon);

        result.DiscountAmount.ShouldBe(30m);
        result.Total.ShouldBe(270m);
    }

    [Fact]
    public void Calculate_WithFixedAmountCoupon_AppliesCorrectDiscount()
    {
        var coupon = new CouponInfo("ELLI", CouponType.FixedAmount, 50m, MinCartTotal: 300m);

        var result = CartCalculator.Calculate([Line(400m)], coupon);

        result.DiscountAmount.ShouldBe(50m);
        result.Total.ShouldBe(350m);
    }

    [Fact]
    public void Calculate_WhenFixedCouponExceedsSubTotal_TotalIsZero()
    {
        // 40₺ sepete 100₺ kupon. İndirim ara toplamda durmalı (40₺), toplam
        // eksiye düşmemeli. ŞippingCalculator.Calculate(0m) == 0m olduğu için
        // (Faz 1'den beri var ve testli) indirimli tutar 0'a inince kargo da
        // bedava — kılavuzun "sadece kargo kalır" beklentisi YANLIŞ (K3/T6).
        var coupon = new CouponInfo("YUZ", CouponType.FixedAmount, 100m, MinCartTotal: 0m);

        var result = CartCalculator.Calculate([Line(40m)], coupon);

        result.DiscountAmount.ShouldBe(40m);
        (result.SubTotal - result.DiscountAmount).ShouldBe(0m);
        result.ShippingCost.ShouldBe(0m);
        result.Total.ShouldBe(0m);
        result.Total.ShouldBeGreaterThanOrEqualTo(0m);
    }

    [Fact]
    public void Calculate_WhenMinimumCartTotalNotMet_CouponIsIgnored()
    {
        var coupon = new CouponInfo("KITAP50", CouponType.FixedAmount, 50m, MinCartTotal: 300m);

        var result = CartCalculator.Calculate([Line(250m)], coupon);

        result.DiscountAmount.ShouldBe(0m);
        result.Total.ShouldBe(250m);
    }

    [Fact]
    public void Calculate_WithDiscountedProductAndCoupon_AppliesBoth()
    {
        // Ürünün indirimli fiyatı zaten satıra girmiş durumda (120₺).
        // Kupon onun üstüne %10 daha uyguluyor.
        var coupon = new CouponInfo("ON", CouponType.Percentage, 10m, MinCartTotal: 0m);

        var result = CartCalculator.Calculate([Line(120m, 2)], coupon);

        result.SubTotal.ShouldBe(240m);
        result.DiscountAmount.ShouldBe(24m);
        result.ShippingCost.ShouldBe(0m);    // 216 ≥ 200
        result.Total.ShouldBe(216m);
    }

    // ── Yuvarlama ────────────────────────────────────────────

    [Fact]
    public void Calculate_RoundsToTwoDecimalsAwayFromZero()
    {
        // 33.33 × 3 = 99.99 ; %15 indirim = 14.9985 → 15.00
        var coupon = new CouponInfo("ONBES", CouponType.Percentage, 15m, MinCartTotal: 0m);

        var result = CartCalculator.Calculate([Line(33.33m, 3)], coupon);

        result.SubTotal.ShouldBe(99.99m);
        result.DiscountAmount.ShouldBe(15.00m);
        // .NET varsayılanı bankacı yuvarlaması olsaydı 14.99 çıkardı.
    }

    [Fact]
    public void Calculate_TotalAlwaysEqualsSubTotalMinusDiscountPlusShipping()
    {
        // Değişmez (invariant) testi: hangi girdiyle olursa olsun formül tutmalı.
        var cases = new (decimal Price, int Qty, CouponInfo? Coupon)[]
        {
            (100m, 1, null),
            (500m, 2, new CouponInfo("A", CouponType.Percentage, 25m, 0m)),
            (45m, 3, new CouponInfo("B", CouponType.FixedAmount, 1000m, 0m)),
            (19.99m, 7, new CouponInfo("C", CouponType.Percentage, 33m, 100m))
        };

        foreach (var (price, qty, coupon) in cases)
        {
            var r = CartCalculator.Calculate([Line(price, qty)], coupon);

            r.Total.ShouldBe(r.SubTotal - r.DiscountAmount + r.ShippingCost);
            r.DiscountAmount.ShouldBeLessThanOrEqualTo(r.SubTotal);
            r.Total.ShouldBeGreaterThanOrEqualTo(0m);
        }
    }

    [Fact]
    public void Calculate_WhenDiscountZeroesOutSubTotal_ShippingAndTotalAreZero()
    {
        // %100'lük bir kupon ara toplamı sıfırlarsa kargo da sıfır olmalı
        // (ShippingCalculator.Calculate(0m) == 0m, Faz 1'den beri kilitli). T6.
        var coupon = new CouponInfo("YUZDEYUZ", CouponType.Percentage, 100m, MinCartTotal: 0m);

        var result = CartCalculator.Calculate([Line(150m)], coupon);

        result.DiscountAmount.ShouldBe(150m);
        result.ShippingCost.ShouldBe(0m);
        result.Total.ShouldBe(0m);
    }

    [Fact]
    public void Calculate_WithNegativeCouponValue_ProducesNoDiscount()
    {
        // Bozuk/negatif bir kupon değeri Math.Clamp'in ALT sınırıyla (0m) durdurulmalı;
        // aksi hâlde negatif indirim toplamı ARTIRIR.
        var coupon = new CouponInfo("BOZUK", CouponType.FixedAmount, -50m, MinCartTotal: 0m);

        var result = CartCalculator.Calculate([Line(100m)], coupon);

        result.DiscountAmount.ShouldBe(0m);
        result.Total.ShouldBe(129.90m);   // 100 + kargo, kupon toplamı artırmadı
    }
}
