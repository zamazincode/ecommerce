using Commerce.Domain.Catalog;

namespace Commerce.Api.Features.Catalog;

public static class ProductSorting
{
    /// İZİN VERİLEN ALANLAR (beyaz liste). Kullanıcının gönderdiği string'i
    /// doğrudan sıralamaya verme — bilinmeyen değer varsayılana düşer,
    /// hata fırlatmaz.
    public static readonly string[] AllowedFields = ["price", "name", "newest"];

    public static IQueryable<Product> ApplySort(
        IQueryable<Product> query, string? sortBy, string? sortDir)
    {
        var descending = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

        var ordered = (sortBy?.Trim().ToLowerInvariant()) switch
        {
            "price" => descending
                ? query.OrderByDescending(p => p.DiscountedPrice ?? p.Price)
                : query.OrderBy(p => p.DiscountedPrice ?? p.Price),

            "name" => descending
                ? query.OrderByDescending(p => p.Name)
                : query.OrderBy(p => p.Name),

            "newest" => descending
                ? query.OrderBy(p => p.CreatedAt)          // desc = en eski
                : query.OrderByDescending(p => p.CreatedAt),

            // Bilinmeyen/boş → varsayılan sıralama. Hiçbir string SQL'e girmiyor;
            // LINQ ifade ağacı kuruyoruz, string birleştirme yok.
            _ => query.OrderByDescending(p => p.CreatedAt)
        };

        // KRİTİK: İkincil sıralama olmadan sayfalama bozulur.
        // Aynı CreatedAt'e sahip 5 ürün varsa Postgres sıralarını her sorguda
        // farklı verebilir; sayfa 1'de gördüğün ürünü sayfa 2'de tekrar görürsün.
        return ordered.ThenBy(p => p.Id);
    }
}
