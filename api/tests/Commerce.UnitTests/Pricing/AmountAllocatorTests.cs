using Commerce.Domain.Pricing;
using Shouldly;

namespace Commerce.UnitTests.Pricing;

public class AmountAllocatorTests
{
    // ── K1 invariantı: Σsonuç HER ZAMAN total'a eşit ────────────

    [Theory]
    [InlineData(new double[] { 300.0, 200.0, 0.0 }, 119.90)]     // kupon + kargo, üç satır
    [InlineData(new double[] { 50.0 }, 50.0)]                    // tek satır
    [InlineData(new double[] { 1.0, 1.0, 1.0 }, 300.0)]          // eşit satırlar
    [InlineData(new double[] { 1.0, 1.0 }, 0.01)]                // her satıra 1 kuruştan az düşen sınır durum
    [InlineData(new double[] { 12345.67, 987.65, 1.0 }, 999999.99)] // büyük tutar
    public void Distribute_SumAlwaysEqualsTotal(double[] weightsRaw, double totalRaw)
    {
        var weights = weightsRaw.Select(w => (decimal)w).ToList();
        var total = (decimal)totalRaw;

        var result = AmountAllocator.Distribute(weights, total);

        result.Sum().ShouldBe(total);
    }

    [Fact]
    public void Distribute_WithoutRemainder_SplitsProportionally()
    {
        var result = AmountAllocator.Distribute([100m, 200m], 300m);

        result.ShouldBe([100m, 200m]);
    }

    [Fact]
    public void Distribute_PutsRemainderOnLargestLine()
    {
        // 100 / 3 = 33.33... — kuruş artığı deterministik olarak SON (en büyük
        // paylı) satıra gidiyor.
        var result = AmountAllocator.Distribute([1m, 1m, 1m], 100m);

        result.ShouldBe([33.33m, 33.33m, 33.34m]);
        result.Sum().ShouldBe(100m);
    }

    [Fact]
    public void Distribute_WithSingleLine_ReturnsTotal()
    {
        var result = AmountAllocator.Distribute([77m], 119.90m);

        result.ShouldBe([119.90m]);
    }

    [Fact]
    public void Distribute_WithZeroWeights_PutsAllOnFirstLine()
    {
        var result = AmountAllocator.Distribute([0m, 0m, 0m], 100m);

        result.ShouldBe([100m, 0m, 0m]);
    }

    [Fact]
    public void Distribute_NeverProducesNegative()
    {
        var result = AmountAllocator.Distribute([500m, 1m, 250m], 119.90m);

        result.ShouldAllBe(r => r >= 0m);
    }
}
