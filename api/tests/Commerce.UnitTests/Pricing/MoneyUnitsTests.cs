using System.Globalization;
using Commerce.Domain.Pricing;
using Shouldly;

namespace Commerce.UnitTests.Pricing;

public class MoneyUnitsTests
{
    [Theory]
    [InlineData(100.00, 10000)]
    [InlineData(100.50, 10050)]
    [InlineData(0.01, 1)]
    [InlineData(1234.56, 123456)]
    [InlineData(0.005, 1)]      // yukarı yuvarlanır (AwayFromZero)
    public void ToMinorUnits_ConvertsCorrectly(decimal amount, long expected)
    {
        MoneyUnits.ToMinorUnits(amount).ShouldBe(expected);
    }

    [Fact]
    public void MinorUnits_RoundTripsWithoutLoss()
    {
        foreach (var amount in new[] { 0.01m, 1m, 99.99m, 1234.56m, 29.90m })
            MoneyUnits.FromMinorUnits(MoneyUnits.ToMinorUnits(amount)).ShouldBe(amount);
    }

    [Fact]
    public void ToProviderString_UsesDotSeparator_RegardlessOfCurrentCulture()
    {
        // BU TESTİ MUTLAKA YAZ.
        // Faz 0'da InvariantGlobalization=false yaptık, yani tr-TR etkin olabilir.
        // Türkçe kültürde ondalık ayıraç VİRGÜL — "100,50" gönderirsen
        // sağlayıcı bunu reddedebilir veya farklı yorumlayabilir.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            MoneyUnits.ToProviderString(100.50m).ShouldBe("100.50");
            MoneyUnits.ToProviderString(1234.5m).ShouldBe("1234.50");
            MoneyUnits.ToProviderString(29.90m).ShouldBe("29.90");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void ParseProviderAmount_ReadsDotSeparator_UnderTurkishCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            // tr-TR'de "100.50" yanlışlıkla 10050 olarak okunabilirdi.
            MoneyUnits.ParseProviderAmount("100.50").ShouldBe(100.50m);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void ParseProviderAmount_WithGarbage_Throws()
    {
        Should.Throw<FormatException>(() => MoneyUnits.ParseProviderAmount("abc"));
        Should.Throw<FormatException>(() => MoneyUnits.ParseProviderAmount(null));
    }

    [Theory]
    [InlineData("100,50")]
    [InlineData("209,7")]
    [InlineData("1,234.56")]
    public void ParseProviderAmount_WithCommaSeparator_Throws(string value)
    {
        // FAZ 3B'NİN 100× HATASININ AYNISI TEKRARLANAMAZ.
        // NumberStyles.Number kullanılsaydı bu girdiler sessizce 10050 / 2097 /
        // 1234.56 olarak ayrıştırılırdı (binlik ayracı virgül sanılır).
        // AllowDecimalPoint ile aynı girdi FormatException atar — gürültülü hata,
        // sessiz yanlış tahsilat yok.
        Should.Throw<FormatException>(() => MoneyUnits.ParseProviderAmount(value));
    }
}
