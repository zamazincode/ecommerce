using Commerce.Domain.Common;

namespace Commerce.Api.Persistence.Seeding.Import;

/// Slug'lar veritabanında unique. Kaynakta aynı adı taşıyan farklı ürünler
/// ("Reyhan", "Sessizlik", "Aya Yolculuk") ve slug üretilemeyen adlar
/// (tamamı Kiril olan 3 başlık) var. Bu sınıf ikisini de çözer.
///
/// Kullanılmış slug'lar önceden <see cref="Reserve"/> ile bildirilir; böylece
/// katalog temizlenmeden çalıştırıldığında mevcut ürünlerin slug'ları da
/// hesaba katılır.
public sealed class SlugRegistry
{
    private readonly HashSet<string> _used = new(StringComparer.OrdinalIgnoreCase);

    public void Reserve(string slug)
    {
        if (!string.IsNullOrWhiteSpace(slug)) _used.Add(slug);
    }

    public bool IsTaken(string slug) => _used.Contains(slug);

    /// <paramref name="fallbackKey"/> hem slug üretilemeyen adlar hem de
    /// çakışma için kullanılır. SKU verildiğinde sonuç satır sırasından
    /// bağımsızdır: aynı ürün her aktarımda aynı slug'ı alır.
    public string Allocate(string name, string? fallbackKey = null, string emptyFallback = "urun")
    {
        var slug = SlugGenerator.Generate(name);

        // Adın tamamı slug'a girmeyen karakterlerden oluşabilir — kaynakta
        // 3 başlık tamamen Kiril ve SlugGenerator boş string döndürüyor.
        if (slug.Length == 0)
            slug = fallbackKey is null
                ? emptyFallback
                : $"{emptyFallback}-{SlugGenerator.Generate(fallbackKey)}";

        if (slug.Length == 0) slug = emptyFallback;

        if (_used.Add(slug)) return slug;

        // Çakışma: önce ürüne özgü sabit bir ek dene (SKU'nun son 6 hanesi).
        if (fallbackKey is { Length: > 0 })
        {
            var suffix = SlugGenerator.Generate(fallbackKey);
            if (suffix.Length > 6) suffix = suffix[^6..];

            if (suffix.Length > 0 && _used.Add($"{slug}-{suffix}"))
                return $"{slug}-{suffix}";
        }

        // Son çare: artan sayı.
        var counter = 2;
        while (!_used.Add($"{slug}-{counter}")) counter++;
        return $"{slug}-{counter}";
    }
}
