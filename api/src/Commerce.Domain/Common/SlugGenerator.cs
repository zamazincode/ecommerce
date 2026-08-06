using System.Text;
using System.Text.RegularExpressions;

namespace Commerce.Domain.Common;

public static partial class SlugGenerator
{
    private static readonly (char From, char To)[] TurkishMap =
    [
        ('ç', 'c'), ('Ç', 'C'),
        ('ğ', 'g'), ('Ğ', 'G'),
        ('ı', 'i'), ('İ', 'I'),
        ('ö', 'o'), ('Ö', 'O'),
        ('ş', 's'), ('Ş', 'S'),
        ('ü', 'u'), ('Ü', 'U')
    ];

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugChars();

    public static string Generate(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            var mapped = ch;
            foreach (var (from, to) in TurkishMap)
            {
                if (ch != from) continue;
                mapped = to;
                break;
            }
            sb.Append(mapped);
        }

        var slug = sb.ToString().ToLowerInvariant();
        slug = NonSlugChars().Replace(slug, "-");
        return slug.Trim('-');
    }
}