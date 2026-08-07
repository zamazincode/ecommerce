using System.Globalization;
using System.Text.Json;
using Commerce.Domain.Common;

namespace Commerce.Api.Persistence.Seeding.Import;

/// Ham metni tiplenmiş değere çeviren SAF fonksiyonlar.
/// Veritabanı, dosya, DI yok — birim testlerin asıl hedefi burası.
public static class ExcelRowParser
{
    /// Kaynak, Python tarafında üretilmiş: boş hücreler literal "None"
    /// (bazen "nan") yazıyor. Metin sanıp veritabanına yazarsak açıklaması
    /// "None" olan ürünler oluşur.
    public static string? Text(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var trimmed = raw.Trim();
        return trimmed is "None" or "nan" or "NaN" or "null" ? null : trimmed;
    }

    /// Fiyatlar kaynakta "122.23" — nokta ayraçlı. Sunucunun kültürü tr-TR
    /// olduğunda CurrentCulture ile ayrıştırmak 12223 üretir; bu yüzden
    /// InvariantCulture ZORUNLU.
    ///
    /// Binlik ayracı BİLEREK kabul edilmiyor (NumberStyles.Number yerine
    /// açıkça AllowDecimalPoint): "209,7" gibi yanlış kültürle biçimlenmiş
    /// bir değer sessizce 2097 olmasın, ayrıştırma başarısız olup satır
    /// rapora düşsün.
    public static decimal? Decimal(string? raw)
    {
        const NumberStyles styles =
            NumberStyles.AllowLeadingWhite |
            NumberStyles.AllowTrailingWhite |
            NumberStyles.AllowLeadingSign |
            NumberStyles.AllowDecimalPoint;

        var text = Text(raw);
        if (text is null) return null;

        return decimal.TryParse(text, styles, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    public static int? Integer(string? raw)
    {
        var text = Text(raw);
        if (text is null) return null;

        return int.TryParse(
            text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    /// Kaynakta iki fiyat var: liste (üzeri çizili) ve satış (ödenen).
    /// Şemada Price = liste, DiscountedPrice = satış — ama sadece gerçekten
    /// indirim varsa. 1470 üründe ikisi eşit; hepsine indirim yazarsak
    /// katalog baştan aşağı "indirimli" görünür.
    ///
    /// Kaynakta satış fiyatı 0 olan 10 satır var (scrape hatası) — o durumda
    /// liste fiyatına düşülür, indirim yazılmaz.
    public static (decimal? Price, decimal? DiscountedPrice) Prices(string? listPrice, string? salePrice)
    {
        var list = Decimal(listPrice);
        var sale = Decimal(salePrice);

        // Liste fiyatı yoksa satış fiyatı tek başına listedir.
        if (list is null or <= 0) return (sale > 0 ? sale : null, null);

        if (sale is null or <= 0 || sale >= list) return (list, null);

        return (list, sale);
    }

    /// D&R "Kategori Yolu"nu 4 ayrı kolonda da veriyor. Ayrı kolonları
    /// kullanıyoruz — yol metnindeki " > " ayracı kategori adının içinde de
    /// geçebilir ("Sözlük, Atlas, İmla Kılavuzu" gibi adlar riskli).
    public static IReadOnlyList<string> CategoryPath(params string?[] segments)
    {
        var path = new List<string>(segments.Length);

        foreach (var segment in segments)
        {
            var name = Text(segment);
            // Ara seviye boşsa alt seviye de anlamsız — yol orada biter.
            if (name is null) break;
            path.Add(name);
        }

        return path;
    }

    /// "Katkıda Bulunanlar" kolonu bir JSON dizisi:
    /// [{"label":"Yazar","kind":"yazar","id":1,"name":"..."}, ...]
    ///
    /// Kaynakta 38 farklı rol var (Çevirmen, Editör, Resimleyen, Kapak
    /// Tasarımı...). ProductAuthor'da rol kolonu olmadığı için SADECE
    /// label == "Yazar" alınır; çevirmeni yazar diye kaydedersek arama
    /// sonuçları yanıltıcı olur.
    ///
    /// JSON hiç yoksa ya da içinde yazar yoksa <paramref name="authorColumn"/>
    /// (düz "Yazar" kolonu) yedek olarak kullanılır — müzik albümlerinde
    /// sanatçı orada duruyor.
    public static IReadOnlyList<string> AuthorNames(string? contributorsJson, string? authorColumn)
    {
        var names = new List<string>();
        var json = Text(contributorsJson);

        if (json is not null)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in document.RootElement.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object) continue;

                        var label = item.TryGetProperty("label", out var l) ? l.GetString() : null;
                        if (!string.Equals(label, "Yazar", StringComparison.OrdinalIgnoreCase)) continue;

                        var name = item.TryGetProperty("name", out var n) ? Text(n.GetString()) : null;
                        if (name is not null && !names.Contains(name)) names.Add(name);
                    }
                }
            }
            catch (JsonException)
            {
                // Bozuk JSON satırı aktarımı durdurmasın; yedeğe düşülür.
            }
        }

        if (names.Count == 0 && Text(authorColumn) is { } fallback)
            names.Add(fallback);

        return names;
    }

    /// "Tüm Görseller" kolonu " | " ile ayrılmış URL listesi.
    public static IReadOnlyList<string> ImageUrls(string? allImages, string? mainImage)
    {
        var source = Text(allImages) ?? Text(mainImage);
        if (source is null) return [];

        var urls = new List<string>();
        foreach (var part in source.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var url = Text(part);
            if (url is null || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;
            if (!urls.Contains(url)) urls.Add(url);
        }

        return urls;
    }

    /// Kaynakta İngilizce: "Turkish", "English"...
    public static string? Language(string? raw) => Text(raw) switch
    {
        null => null,
        "Turkish" => "Türkçe",
        "English" => "İngilizce",
        "German" => "Almanca",
        "French" => "Fransızca",
        "Arabic" => "Arapça",
        "Russian" => "Rusça",
        "Spanish" => "İspanyolca",
        var other => other
    };

    /// Kaynakta "Paperback"/"Hardcover"; tanımadığımız bir şey gelirse
    /// uydurmak yerine Unknown yazılır.
    public static BookBinding Binding(string? raw) => Text(raw) switch
    {
        "Paperback" => BookBinding.Paperback,
        "Hardcover" => BookBinding.Hardcover,
        "Ebook" or "E-Book" => BookBinding.Ebook,
        _ => BookBinding.Unknown
    };
}
