using Commerce.Api.Persistence.Seeding.Import;
using Shouldly;

namespace Commerce.UnitTests.Import;

public class SyntheticValuesTests
{
    private static readonly DateTime Now = new(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void StableHash_IsSameAcrossCalls()
    {
        // string.GetHashCode süreç başına rastgeleleştirilir; onu kullansaydık
        // her aktarımda stok ve tarih değişirdi.
        SyntheticValues.StableHash("0001899648001")
            .ShouldBe(SyntheticValues.StableHash("0001899648001"));
    }

    [Fact]
    public void StockFor_IsDeterministic()
    {
        SyntheticValues.StockFor("0001899648001")
            .ShouldBe(SyntheticValues.StockFor("0001899648001"));
    }

    [Fact]
    public void StockFor_StaysWithinRange()
    {
        for (var i = 0; i < 500; i++)
            SyntheticValues.StockFor($"000000{i:D7}").ShouldBeInRange(0, 120);
    }

    [Fact]
    public void StockFor_ProducesSomeOutOfStockProducts()
    {
        // "Tükendi" senaryosu ve stok filtresi test edilebilir kalmalı.
        var outOfStock = Enumerable.Range(0, 1000)
            .Count(i => SyntheticValues.StockFor($"000000{i:D7}") == 0);

        outOfStock.ShouldBeGreaterThan(0);
        outOfStock.ShouldBeLessThan(300);
    }

    [Fact]
    public void CreatedAtFor_IsDeterministicAndUtc()
    {
        var first = SyntheticValues.CreatedAtFor("0001899648001", Now);
        var second = SyntheticValues.CreatedAtFor("0001899648001", Now);

        first.ShouldBe(second);
        first.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void CreatedAtFor_StaysWithinLastTwoYears()
    {
        for (var i = 0; i < 200; i++)
        {
            var createdAt = SyntheticValues.CreatedAtFor($"000000{i:D7}", Now);

            createdAt.ShouldBeLessThanOrEqualTo(Now);
            createdAt.ShouldBeGreaterThan(Now.AddDays(-732));
        }
    }

    [Fact]
    public void CreatedAtFor_SpreadsProductsOverTime()
    {
        // Hepsi aynı ana düşerse "yeniler önce" sıralaması anlamsızlaşır.
        var distinctDays = Enumerable.Range(0, 300)
            .Select(i => SyntheticValues.CreatedAtFor($"000000{i:D7}", Now).Date)
            .Distinct()
            .Count();

        distinctDays.ShouldBeGreaterThan(100);
    }
}
