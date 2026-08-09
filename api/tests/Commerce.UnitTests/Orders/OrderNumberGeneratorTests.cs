using System.Text.RegularExpressions;
using Commerce.Domain.Orders;
using Shouldly;

namespace Commerce.UnitTests.Orders;

public class OrderNumberGeneratorTests
{
    private static readonly DateTime Now = new(2026, 8, 6, 14, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Generate_ProducesExpectedFormat()
    {
        var number = OrderNumberGenerator.Generate(Now);

        Regex.IsMatch(number, @"^ORD-20260806-[A-Z2-9]{6}$").ShouldBeTrue(
            $"Beklenmeyen format: {number}");
    }

    [Fact]
    public void Generate_DoesNotContainAmbiguousCharacters()
    {
        // 0/O ve 1/I telefonda okunurken karıştırılır.
        var numbers = Enumerable.Range(0, 500)
            .Select(_ => OrderNumberGenerator.Generate(Now)[13..])   // sadece son ek
            .ToList();

        numbers.ShouldAllBe(s => !s.Contains('O') && !s.Contains('0'));
        numbers.ShouldAllBe(s => !s.Contains('I') && !s.Contains('1'));
    }

    [Fact]
    public void Generate_ProducesUniqueValues()
    {
        // 32^6 ≈ 1,07 milyar. 10.000 üretimde EN AZ BİR çakışma olasılığı %4,5
        // (doğum günü paradoksu) — o test 22 koşuda bir kırmızı yanardı.
        // 1.000 üretimde beklenen çakışma 0,0005; bir taneye tolerans bırakıyoruz.
        var numbers = Enumerable.Range(0, 1_000)
            .Select(_ => OrderNumberGenerator.Generate(Now)).ToList();

        numbers.Distinct().Count().ShouldBeGreaterThanOrEqualTo(999);
    }

    [Fact]
    public void Generate_UsesProvidedDate_NotSystemClock()
    {
        var past = new DateTime(2020, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        OrderNumberGenerator.Generate(past).ShouldStartWith("ORD-20200115-");
    }
}
