using System.Globalization;
using Commerce.Api.Persistence.Seeding.Import;
using Commerce.Domain.Common;
using Shouldly;

namespace Commerce.UnitTests.Import;

public class ExcelRowParserTests
{
    [Theory]
    [InlineData("None")]
    [InlineData("  None  ")]
    [InlineData("nan")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Text_TreatsSourcePlaceholdersAsNull(string? raw)
    {
        // Kaynak Python tarafında üretildi: boş hücreler literal "None" yazıyor.
        ExcelRowParser.Text(raw).ShouldBeNull();
    }

    [Fact]
    public void Text_TrimsRealValue()
    {
        ExcelRowParser.Text("  Suç ve Ceza  ").ShouldBe("Suç ve Ceza");
    }

    [Fact]
    public void Decimal_UsesInvariantCulture_EvenWhenThreadCultureIsTurkish()
    {
        // tr-TR'de ondalık ayracı virgül. CurrentCulture ile ayrıştırsaydık
        // "122.23" değeri 12223 olurdu — 100 kat yanlış fiyat.
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

        try
        {
            ExcelRowParser.Decimal("122.23").ShouldBe(122.23m);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Decimal_WhenUnparsable_ReturnsNull()
    {
        ExcelRowParser.Decimal("bilinmiyor").ShouldBeNull();
        ExcelRowParser.Decimal("None").ShouldBeNull();
    }

    [Fact]
    public void Decimal_RejectsCommaSeparatedValueInsteadOfMisreadingIt()
    {
        // "209,7" yanlış kültürle biçimlenmiş bir ondalıktır. Binlik ayracı
        // kabul etseydik sessizce 2097 olurdu — on kat yanlış fiyat.
        ExcelRowParser.Decimal("209,7").ShouldBeNull();
        ExcelRowParser.Decimal("9.609,02").ShouldBeNull();
    }

    [Fact]
    public void Prices_WhenSaleIsLower_RecordsDiscount()
    {
        var (price, discounted) = ExcelRowParser.Prices("300", "209.7");

        price.ShouldBe(300m);
        discounted.ShouldBe(209.7m);
    }

    [Fact]
    public void Prices_WhenSaleEqualsList_HasNoDiscount()
    {
        // Kaynakta 1470 satır böyle. Hepsine indirim yazarsak katalog
        // baştan aşağı "indirimli" görünür.
        var (price, discounted) = ExcelRowParser.Prices("122.23", "122.23");

        price.ShouldBe(122.23m);
        discounted.ShouldBeNull();
    }

    [Fact]
    public void Prices_WhenSaleIsZero_FallsBackToListPrice()
    {
        // Scrape hatası: 10 satırda satış fiyatı 0 gelmiş.
        var (price, discounted) = ExcelRowParser.Prices("264", "0");

        price.ShouldBe(264m);
        discounted.ShouldBeNull();
    }

    [Fact]
    public void Prices_WhenListMissing_UsesSaleAsPrice()
    {
        var (price, discounted) = ExcelRowParser.Prices("None", "89.9");

        price.ShouldBe(89.9m);
        discounted.ShouldBeNull();
    }

    [Fact]
    public void Prices_WhenBothMissing_ReturnsNull()
    {
        var (price, _) = ExcelRowParser.Prices("None", "None");

        price.ShouldBeNull();
    }

    [Fact]
    public void CategoryPath_StopsAtFirstEmptySegment()
    {
        var path = ExcelRowParser.CategoryPath("Kitap", "Manga", "None", "None");

        path.ShouldBe(["Kitap", "Manga"]);
    }

    [Fact]
    public void CategoryPath_ReadsFourLevels()
    {
        var path = ExcelRowParser.CategoryPath("Kitap", "Edebiyat", "Roman", "Türk Romanı");

        path.Count.ShouldBe(4);
        path[3].ShouldBe("Türk Romanı");
    }

    [Fact]
    public void AuthorNames_TakesOnlyAuthorLabels()
    {
        // Kaynakta 38 farklı rol var; çevirmeni yazar diye kaydedersek
        // "yazarı Ekrem Demirli" gibi yanlış sonuçlar çıkar.
        const string json = """
            [
              {"label": "Yazar", "kind": "yazar", "name": "Muhyiddin İbnü'l-Arabi"},
              {"label": "Çevirmen", "kind": "yazar", "name": "Ekrem Demirli"},
              {"label": "Yayınevi", "kind": "yayinevi", "name": "Fikriyat"}
            ]
            """;

        ExcelRowParser.AuthorNames(json, "Muhyiddin İbnü'l-Arabi")
            .ShouldBe(["Muhyiddin İbnü'l-Arabi"]);
    }

    [Fact]
    public void AuthorNames_KeepsMultipleAuthors()
    {
        const string json = """
            [
              {"label": "Yazar", "kind": "yazar", "name": "Lisa Jane Gillespie"},
              {"label": "Yazar", "kind": "yazar", "name": "Alex Frith"}
            ]
            """;

        ExcelRowParser.AuthorNames(json, null)
            .ShouldBe(["Lisa Jane Gillespie", "Alex Frith"]);
    }

    [Fact]
    public void AuthorNames_WhenNoAuthorLabel_FallsBackToAuthorColumn()
    {
        // Müzik albümlerinde katkıda bulunan "Sanatçı" rolünde; düz Yazar
        // kolonunda ise isim duruyor.
        const string json = """[{"label": "Sanatçı", "kind": "yazar", "name": "Nazan Öncel"}]""";

        ExcelRowParser.AuthorNames(json, "Nazan Öncel").ShouldBe(["Nazan Öncel"]);
    }

    [Fact]
    public void AuthorNames_WhenJsonBroken_FallsBackInsteadOfThrowing()
    {
        ExcelRowParser.AuthorNames("{bozuk", "Sabahattin Ali").ShouldBe(["Sabahattin Ali"]);
    }

    [Fact]
    public void AuthorNames_WhenNothingAvailable_ReturnsEmpty()
    {
        ExcelRowParser.AuthorNames("None", "None").ShouldBeEmpty();
    }

    [Fact]
    public void ImageUrls_SplitsOnPipeAndKeepsOrder()
    {
        const string raw = "https://i.dr.com.tr/a-1.jpg | https://i.dr.com.tr/a-2.jpg";

        ExcelRowParser.ImageUrls(raw, null)
            .ShouldBe(["https://i.dr.com.tr/a-1.jpg", "https://i.dr.com.tr/a-2.jpg"]);
    }

    [Fact]
    public void ImageUrls_FallsBackToMainImage()
    {
        ExcelRowParser.ImageUrls("None", "https://i.dr.com.tr/tek.jpg")
            .ShouldBe(["https://i.dr.com.tr/tek.jpg"]);
    }

    [Fact]
    public void ImageUrls_DropsDuplicatesAndNonUrls()
    {
        const string raw = "https://a.jpg | https://a.jpg | görsel yok";

        ExcelRowParser.ImageUrls(raw, null).ShouldBe(["https://a.jpg"]);
    }

    [Theory]
    [InlineData("Turkish", "Türkçe")]
    [InlineData("English", "İngilizce")]
    [InlineData("Klingonca", "Klingonca")]
    [InlineData("None", null)]
    public void Language_MapsKnownValues(string raw, string? expected)
    {
        ExcelRowParser.Language(raw).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Paperback", BookBinding.Paperback)]
    [InlineData("Hardcover", BookBinding.Hardcover)]
    [InlineData("None", BookBinding.Unknown)]
    [InlineData("Spiralli", BookBinding.Unknown)]
    public void Binding_MapsKnownValuesAndFallsBackToUnknown(string raw, BookBinding expected)
    {
        ExcelRowParser.Binding(raw).ShouldBe(expected);
    }
}
