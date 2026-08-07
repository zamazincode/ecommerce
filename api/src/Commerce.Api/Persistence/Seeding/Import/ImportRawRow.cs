namespace Commerce.Api.Persistence.Seeding.Import;

/// Excel'den okunan HAM satır: kolon başlığı → hücre metni.
/// Hiçbir dönüşüm yapılmamıştır; boş hücreler kaynakta literal "None" yazıyor
/// olabilir, onu <see cref="ExcelRowParser.Text"/> temizler.
///
/// Importer dosya değil bu tipin listesini alır — böylece testler Excel
/// dosyası üretmek zorunda kalmadan aktarımı uçtan uca çalıştırabilir.
public sealed class ImportRawRow
{
    private readonly IReadOnlyDictionary<string, string?> _cells;

    public ImportRawRow(IReadOnlyDictionary<string, string?> cells)
    {
        // Başlıklarda görünmez fark olabilir; büyük/küçük harfe takılmayalım.
        _cells = new Dictionary<string, string?>(cells, StringComparer.OrdinalIgnoreCase);
    }

    /// Kaynaktaki satır numarası (1 tabanlı, başlık satırı dahil).
    /// Sadece hata raporunda "hangi satır atlandı" demek için var.
    public int SourceLineNumber { get; init; }

    public string? this[string column] => _cells.GetValueOrDefault(column);
}
