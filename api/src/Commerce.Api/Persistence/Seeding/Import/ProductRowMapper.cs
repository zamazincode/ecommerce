namespace Commerce.Api.Persistence.Seeding.Import;

/// D&R scrape çıktısındaki kolon başlıkları.
/// Kaynak dosya değişirse düzeltilecek TEK yer burası.
public static class ImportColumns
{
    public const string Sku = "Ürün No (SKU)";
    public const string Barcode = "Barkod";
    public const string Title = "Başlık";
    public const string Brand = "Marka";
    public const string Publisher = "Yayınevi";
    public const string Author = "Yazar";
    public const string Contributors = "Katkıda Bulunanlar";
    public const string Category1 = "Kategori 1";
    public const string Category2 = "Kategori 2";
    public const string Category3 = "Kategori 3";
    public const string Category4 = "Kategori 4";
    public const string SalePrice = "Satış Fiyatı";
    public const string ListPrice = "Liste Fiyatı";
    public const string PageCount = "Sayfa Sayısı";
    public const string PublishedYear = "Basım Yılı";
    public const string Language = "Dil";
    public const string Binding = "Cilt Tipi";
    public const string MainImage = "Görsel";
    public const string AllImages = "Tüm Görseller";
    public const string DescriptionText = "Açıklama (Metin)";
}

/// Ham satırı <see cref="ProductImportRow"/>'a çevirir; çeviremiyorsa
/// nedenini söyler. Atılan satır sessizce kaybolmaz, rapora yazılır.
public static class ProductRowMapper
{
    /// Kategori ağacının kitap kökü. BookDetail sadece bu kökün altındaki
    /// ürünlere açılır.
    private const string BookRootCategory = "Kitap";

    public static bool TryMap(
        ImportRawRow raw,
        out ProductImportRow row,
        out string? skipReason)
    {
        row = null!;
        skipReason = null;

        var sku = ExcelRowParser.Text(raw[ImportColumns.Sku]);
        if (sku is null)
        {
            skipReason = "SKU boş";
            return false;
        }

        // Kaynakta 4 satırın başlığı boş (kırık scrape). Adsız ürün
        // kataloğa girmemeli — slug da üretilemez.
        var name = ExcelRowParser.Text(raw[ImportColumns.Title]);
        if (name is null)
        {
            skipReason = "Başlık boş";
            return false;
        }

        var categoryPath = ExcelRowParser.CategoryPath(
            raw[ImportColumns.Category1],
            raw[ImportColumns.Category2],
            raw[ImportColumns.Category3],
            raw[ImportColumns.Category4]);

        if (categoryPath.Count == 0)
        {
            skipReason = "Kategori boş";
            return false;
        }

        var (price, discountedPrice) =
            ExcelRowParser.Prices(raw[ImportColumns.ListPrice], raw[ImportColumns.SalePrice]);

        if (price is null)
        {
            skipReason = "Fiyat okunamadı";
            return false;
        }

        var isBook = categoryPath[0] == BookRootCategory;

        row = new ProductImportRow
        {
            Sku = sku,
            Name = name,
            Description = ExcelRowParser.Text(raw[ImportColumns.DescriptionText]),
            Price = price.Value,
            DiscountedPrice = discountedPrice,
            CategoryPath = categoryPath,
            PublisherName = ExcelRowParser.Text(raw[ImportColumns.Publisher]),
            BrandName = ExcelRowParser.Text(raw[ImportColumns.Brand]),
            AuthorNames = ExcelRowParser.AuthorNames(
                raw[ImportColumns.Contributors], raw[ImportColumns.Author]),
            ImageUrls = ExcelRowParser.ImageUrls(
                raw[ImportColumns.AllImages], raw[ImportColumns.MainImage]),
            IsBook = isBook,
            Isbn = isBook ? ExcelRowParser.Text(raw[ImportColumns.Barcode]) : null,
            PageCount = isBook ? ExcelRowParser.Integer(raw[ImportColumns.PageCount]) : null,
            PublishedYear = isBook ? ExcelRowParser.Integer(raw[ImportColumns.PublishedYear]) : null,
            Language = isBook ? ExcelRowParser.Language(raw[ImportColumns.Language]) : null,
            Binding = isBook ? ExcelRowParser.Binding(raw[ImportColumns.Binding]) : default
        };

        return true;
    }
}
