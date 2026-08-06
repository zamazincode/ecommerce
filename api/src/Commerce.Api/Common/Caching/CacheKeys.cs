namespace Commerce.Api.Common.Caching;

/// Anahtar kalıbını TEK YERDE tut. Elle string yazarsan er ya da geç
/// bir yerde "product:{slug}" yerine "products:{slug}" yazıp
/// "neden cache çalışmıyor" diye saatlerce ararsın.
public static class CacheKeys
{
    public const string CategoriesFlat = "categories:flat";
    public const string Home = "home:blocks";

    public static string Product(string slug) => $"product:{slug}";
    public static string Suggest(string term) => $"suggest:{term}";
}

/// HybridCache etiketleri. Faz 11'de admin bir ürünü güncellediğinde
/// cache.RemoveByTagAsync(CacheTags.Products) ile tek satırda temizleyeceğiz.
public static class CacheTags
{
    public const string Products = "products";
    public const string Categories = "categories";
}

public static class CacheDurations
{
    public static readonly TimeSpan CategoryTree = TimeSpan.FromHours(1);
    public static readonly TimeSpan Home = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan ProductDetail = TimeSpan.FromMinutes(10);
}
