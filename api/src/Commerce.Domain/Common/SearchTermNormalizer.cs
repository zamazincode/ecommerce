using System.Text;

namespace Commerce.Domain.Common;

/// Arama terimini veritabanındaki aksansız/küçük harfli biçime çevirir.
/// Vektör immutable_unaccent(lower(...)) ile üretiliyor; sorgu tarafı da
/// aynı dönüşümden geçmeli, yoksa "suç" hiçbir şey bulamaz.
public static class SearchTermNormalizer
{
    public const int MinLength = 2;
    public const int MaxLength = 100;

    private static readonly (char From, char To)[] Map =
    [
        ('ç', 'c'), ('Ç', 'c'),
        ('ğ', 'g'), ('Ğ', 'g'),
        ('ı', 'i'), ('İ', 'i'), ('I', 'i'),
        ('ö', 'o'), ('Ö', 'o'),
        ('ş', 's'), ('Ş', 's'),
        ('ü', 'u'), ('Ü', 'u'),
        ('â', 'a'), ('î', 'i'), ('û', 'u')
    ];

    public static string Normalize(string? term)
    {
        if (string.IsNullOrWhiteSpace(term)) return string.Empty;

        var trimmed = term.Trim();
        if (trimmed.Length > MaxLength) trimmed = trimmed[..MaxLength];

        var sb = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            var mapped = ch;
            foreach (var (from, to) in Map)
            {
                if (ch != from) continue;
                mapped = to;
                break;
            }

            // tsquery sözdizimini bozabilecek karakterleri boşluğa çevir.
            // plainto_tsquery zaten temizler ama girdiyi biz de daraltıyoruz.
            sb.Append(char.IsLetterOrDigit(mapped) || mapped == ' ' ? mapped : ' ');
        }

        // Birden fazla boşluğu teke indir
        var collapsed = string.Join(' ',
            sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return collapsed.ToLowerInvariant();
    }

    public static bool IsValid(string? term)
        => Normalize(term).Length >= MinLength;
}
