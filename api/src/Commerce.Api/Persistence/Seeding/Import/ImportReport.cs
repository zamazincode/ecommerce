using System.Text;

namespace Commerce.Api.Persistence.Seeding.Import;

/// Aktarımın sonucu. Atlanan satırlar sessizce kaybolmasın diye
/// sebepleriyle birlikte taşınır.
public sealed class ImportReport
{
    public int RowsRead { get; set; }
    public int ProductsCreated { get; set; }
    public int ProductsUpdated { get; set; }

    public int CategoriesCreated { get; set; }
    public int AuthorsCreated { get; set; }
    public int PublishersCreated { get; set; }
    public int BrandsCreated { get; set; }
    public int BookDetailsWritten { get; set; }
    public int ImagesWritten { get; set; }

    public List<(int Line, string Sku, string Reason)> Skipped { get; } = [];

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("─── Aktarım raporu ───────────────────────────");
        sb.AppendLine($"  Okunan satır      : {RowsRead}");
        sb.AppendLine($"  Yeni ürün         : {ProductsCreated}");
        sb.AppendLine($"  Güncellenen ürün  : {ProductsUpdated}");
        sb.AppendLine($"  Atlanan satır     : {Skipped.Count}");
        sb.AppendLine($"  Kategori (yeni)   : {CategoriesCreated}");
        sb.AppendLine($"  Yazar (yeni)      : {AuthorsCreated}");
        sb.AppendLine($"  Yayınevi (yeni)   : {PublishersCreated}");
        sb.AppendLine($"  Marka (yeni)      : {BrandsCreated}");
        sb.AppendLine($"  Kitap detayı      : {BookDetailsWritten}");
        sb.AppendLine($"  Görsel            : {ImagesWritten}");

        if (Skipped.Count > 0)
        {
            sb.AppendLine("  Atlananlar:");
            foreach (var (line, sku, reason) in Skipped.Take(20))
                sb.AppendLine($"    satır {line} (SKU {sku}): {reason}");

            if (Skipped.Count > 20)
                sb.AppendLine($"    ... ve {Skipped.Count - 20} satır daha");
        }

        sb.Append("──────────────────────────────────────────────");
        return sb.ToString();
    }
}
