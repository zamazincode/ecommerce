using System.Globalization;
using Commerce.Api.Persistence.Seeding.Import;
using Shouldly;

namespace Commerce.UnitTests.Import;

public class ExcelWorkbookReaderTests
{
    /// Hücrelerin bir kısmı kaynakta METİN değil SAYI: fiyatlar double,
    /// sayfa sayısı/basım yılı int olarak duruyor. Sunucunun kültürü tr-TR
    /// iken düz ToString() çağırırsak 209.7 değeri "209,7" olur ve aktarım
    /// on kat yanlış fiyat yazar.
    [Fact]
    public void CellText_FormatsNumbersWithInvariantCulture_RegardlessOfThreadCulture()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

        try
        {
            ExcelWorkbookReader.CellText(209.7d).ShouldBe("209.7");
            ExcelWorkbookReader.CellText(9609.02d).ShouldBe("9609.02");
            ExcelWorkbookReader.CellText(122.23m).ShouldBe("122.23");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void CellText_KeepsWholeNumbersClean()
    {
        // Sayfa sayısı "224" olmalı, "224.0" değil.
        ExcelWorkbookReader.CellText(224d).ShouldBe("224");
        ExcelWorkbookReader.CellText(2020).ShouldBe("2020");
    }

    [Fact]
    public void CellText_PassesStringsThrough()
    {
        ExcelWorkbookReader.CellText("9786057784810").ShouldBe("9786057784810");
        ExcelWorkbookReader.CellText(null).ShouldBeNull();
    }

    [Fact]
    public void CellText_FormatsDatesRoundTrippable()
    {
        var date = new DateTime(2026, 8, 6, 16, 7, 15, DateTimeKind.Utc);

        ExcelWorkbookReader.CellText(date).ShouldStartWith("2026-08-06T16:07:15");
    }

    /// Okuyucudan çıkan metin, ayrıştırıcının beklediği biçimde olmalı.
    /// İki katman ayrı ayrı doğru olup birlikte yanlış çalışabiliyordu.
    [Fact]
    public void CellText_OutputIsParsableByRowParser()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

        try
        {
            var listPrice = ExcelWorkbookReader.CellText(300d);
            var salePrice = ExcelWorkbookReader.CellText(209.7d);

            var (price, discounted) = ExcelRowParser.Prices(listPrice, salePrice);

            price.ShouldBe(300m);
            discounted.ShouldBe(209.7m);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
