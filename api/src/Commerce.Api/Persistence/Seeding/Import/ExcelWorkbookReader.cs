using System.Globalization;
using System.Text;
using ExcelDataReader;

namespace Commerce.Api.Persistence.Seeding.Import;

/// Aktarımdaki TEK dosya okuma noktası. İş kuralı yok — hücreleri metin
/// olarak alır, başlıklarla eşler, gerisini <see cref="CatalogImporter"/>
/// devralır. Bu ayrım sayesinde importer testlerinde Excel dosyası
/// üretmek gerekmiyor.
public static class ExcelWorkbookReader
{
    /// ExcelDataReader varsayılan yapılandırmasında windows-1252 kodlamasını
    /// çözüyor — .xlsx okurken bile. .NET Core'da o kod sayfası ancak bu
    /// provider kaydedilirse var; yoksa daha dosya açılmadan
    /// "No data is available for encoding 1252" alınır.
    static ExcelWorkbookReader()
        => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static IReadOnlyList<ImportRawRow> Read(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Aktarım dosyası bulunamadı: {filePath}", filePath);

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);

        // İlk satır başlık.
        if (!reader.Read())
            throw new InvalidOperationException($"Dosya boş: {filePath}");

        var headers = new string[reader.FieldCount];
        for (var i = 0; i < reader.FieldCount; i++)
            headers[i] = reader.GetValue(i)?.ToString()?.Trim() ?? $"Kolon{i}";

        var rows = new List<ImportRawRow>();
        var line = 1;

        while (reader.Read())
        {
            line++;

            var cells = new Dictionary<string, string?>(headers.Length, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Length && i < reader.FieldCount; i++)
            {
                // Aynı başlık iki kez geçerse ilki kazanır; kolon adı zaten
                // kaynağın kontrolünde, sessizce ezmek yerine yok sayıyoruz.
                cells.TryAdd(headers[i], CellText(reader.GetValue(i)));
            }

            rows.Add(new ImportRawRow(cells) { SourceLineNumber = line });
        }

        return rows;
    }

    /// Hücreyi metne çevirir — DAİMA InvariantCulture ile.
    ///
    /// Fiyatlar kaynakta metin değil SAYI olarak duruyor; ExcelDataReader
    /// bunları double döndürüyor. Düz .ToString() çağırırsak sunucunun
    /// kültürü devreye girer ve tr-TR'de 209.7 değeri "209,7" olur. Sonraki
    /// adımda invariant ayrıştırıcı virgülü BİNLİK ayracı sayıp 2097 üretir:
    /// on kat yanlış fiyat, üstelik sessizce.
    public static string? CellText(object? value) => value switch
    {
        null => null,
        string text => text,
        DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };
}
