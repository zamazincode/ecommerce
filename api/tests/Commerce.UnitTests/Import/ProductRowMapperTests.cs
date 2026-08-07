using Commerce.Api.Persistence.Seeding.Import;
using Commerce.Domain.Common;
using Shouldly;

namespace Commerce.UnitTests.Import;

public class ProductRowMapperTests
{
    /// Kaynaktaki tipik bir kitap satırı; testler sadece ilgilendikleri
    /// kolonu değiştirsin diye tam bir örnek tutuyoruz.
    private static Dictionary<string, string?> BookCells() => new()
    {
        [ImportColumns.Sku] = "0001899648001",
        [ImportColumns.Barcode] = "9786057784810",
        [ImportColumns.Title] = "İkigami Cilt 3",
        [ImportColumns.Brand] = "None",
        [ImportColumns.Publisher] = "Marmara Çizgi",
        [ImportColumns.Author] = "Motoro Mase",
        [ImportColumns.Contributors] =
            """[{"label":"Yazar","kind":"yazar","name":"Motoro Mase"}]""",
        [ImportColumns.Category1] = "Kitap",
        [ImportColumns.Category2] = "Manga",
        [ImportColumns.Category3] = "None",
        [ImportColumns.Category4] = "None",
        [ImportColumns.ListPrice] = "150",
        [ImportColumns.SalePrice] = "122.23",
        [ImportColumns.PageCount] = "224",
        [ImportColumns.PublishedYear] = "2020",
        [ImportColumns.Language] = "Turkish",
        [ImportColumns.Binding] = "Paperback",
        [ImportColumns.MainImage] = "https://i.dr.com.tr/a-1.jpg",
        [ImportColumns.AllImages] = "https://i.dr.com.tr/a-1.jpg",
        [ImportColumns.DescriptionText] = "Hayat karanlıktır, ölüm de karanlıktır..."
    };

    private static ImportRawRow Row(Dictionary<string, string?> cells)
        => new(cells) { SourceLineNumber = 2 };

    [Fact]
    public void TryMap_MapsBookRow()
    {
        ProductRowMapper.TryMap(Row(BookCells()), out var row, out _).ShouldBeTrue();

        row.Sku.ShouldBe("0001899648001");
        row.Name.ShouldBe("İkigami Cilt 3");
        row.Price.ShouldBe(150m);
        row.DiscountedPrice.ShouldBe(122.23m);
        row.CategoryPath.ShouldBe(["Kitap", "Manga"]);
        row.PublisherName.ShouldBe("Marmara Çizgi");
        row.BrandName.ShouldBeNull();
        row.AuthorNames.ShouldBe(["Motoro Mase"]);
        row.IsBook.ShouldBeTrue();
        row.Isbn.ShouldBe("9786057784810");
        row.PageCount.ShouldBe(224);
        row.PublishedYear.ShouldBe(2020);
        row.Language.ShouldBe("Türkçe");
        row.Binding.ShouldBe(BookBinding.Paperback);
        row.ImageUrls.Count.ShouldBe(1);
    }

    [Fact]
    public void TryMap_NonBookRow_HasNoBookFields()
    {
        // Kaynakta 154 kitap dışı ürün var: puzzle, kulaklık, defter, albüm.
        // Bunlara BookDetail açılmamalı — barkodları ISBN değil.
        var cells = BookCells();
        cells[ImportColumns.Category1] = "Elektronik";
        cells[ImportColumns.Category2] = "Ev Elektroniği";
        cells[ImportColumns.Publisher] = "None";
        cells[ImportColumns.Brand] = "JBL";
        cells[ImportColumns.Language] = "None";
        cells[ImportColumns.Binding] = "None";

        ProductRowMapper.TryMap(Row(cells), out var row, out _).ShouldBeTrue();

        row.IsBook.ShouldBeFalse();
        row.Isbn.ShouldBeNull();
        row.PageCount.ShouldBeNull();
        row.Language.ShouldBeNull();
        row.PublisherName.ShouldBeNull();
        row.BrandName.ShouldBe("JBL");
    }

    [Fact]
    public void TryMap_WhenTitleEmpty_SkipsWithReason()
    {
        // Kaynakta 4 satırın başlığı boş (kırık scrape). Adsız ürün kataloğa
        // girmemeli; slug da üretilemez.
        var cells = BookCells();
        cells[ImportColumns.Title] = "None";

        ProductRowMapper.TryMap(Row(cells), out _, out var reason).ShouldBeFalse();
        reason.ShouldBe("Başlık boş");
    }

    [Fact]
    public void TryMap_WhenSkuEmpty_SkipsWithReason()
    {
        var cells = BookCells();
        cells[ImportColumns.Sku] = "None";

        ProductRowMapper.TryMap(Row(cells), out _, out var reason).ShouldBeFalse();
        reason.ShouldBe("SKU boş");
    }

    [Fact]
    public void TryMap_WhenCategoryEmpty_SkipsWithReason()
    {
        var cells = BookCells();
        cells[ImportColumns.Category1] = "None";

        ProductRowMapper.TryMap(Row(cells), out _, out var reason).ShouldBeFalse();
        reason.ShouldBe("Kategori boş");
    }

    [Fact]
    public void TryMap_WhenBothPricesMissing_SkipsWithReason()
    {
        var cells = BookCells();
        cells[ImportColumns.ListPrice] = "None";
        cells[ImportColumns.SalePrice] = "None";

        ProductRowMapper.TryMap(Row(cells), out _, out var reason).ShouldBeFalse();
        reason.ShouldBe("Fiyat okunamadı");
    }
}
