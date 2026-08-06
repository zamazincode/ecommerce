using Commerce.Api.Common.Caching;
using Commerce.Api.Common.Exceptions;
using Commerce.Api.Features.Catalog.Dtos;
using Commerce.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Commerce.Api.Features.Catalog;

public sealed class CategoryService(AppDbContext db, HybridCache cache)
{
    /// Tüm kategoriler TEK sorguda. Ağacı BELLEKTE kuruyoruz.
    /// Özyinelemeli sorgu atma — 12 kategori için 12 sorgu demek.
    public async Task<IReadOnlyList<CategoryDto>> GetFlatAsync(CancellationToken ct = default)
        => await cache.GetOrCreateAsync(
            CacheKeys.CategoriesFlat,
            db,
            static async (database, token) => (IReadOnlyList<CategoryDto>)await database.Categories
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
                .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.ParentId, c.DisplayOrder))
                .ToListAsync(token),
            new HybridCacheEntryOptions { Expiration = CacheDurations.CategoryTree },
            tags: [CacheTags.Categories],
            cancellationToken: ct);

    public async Task<IReadOnlyList<CategoryTreeDto>> GetTreeAsync(CancellationToken ct = default)
    {
        var flat = await GetFlatAsync(ct);
        return BuildTree(flat, parentId: null);
    }

    private static List<CategoryTreeDto> BuildTree(
        IReadOnlyList<CategoryDto> all, int? parentId)
        => all.Where(c => c.ParentId == parentId)
              .OrderBy(c => c.DisplayOrder)
              .Select(c => new CategoryTreeDto(
                  c.Id, c.Name, c.Slug, c.DisplayOrder, BuildTree(all, c.Id)))
              .ToList();

    /// "Kitap" kategorisini seçen kullanıcı Roman ve Polisiye ürünlerini de
    /// görmeli. Kendisi + tüm alt kategorilerin id'lerini döndürür.
    public async Task<IReadOnlyCollection<int>> GetSelfAndDescendantIdsAsync(
        int categoryId, CancellationToken ct = default)
    {
        var flat = await GetFlatAsync(ct);

        // Kök kategorilerin ParentId'si null. BFS'te null'ı anahtar olarak
        // hiç aramıyoruz (aramaya gerçek bir id ile başlıyoruz), o yüzden
        // sözlükten dışarıda bırakıyoruz — nullable anahtar da gerekmiyor.
        var byParent = flat
            .Where(c => c.ParentId.HasValue)
            .GroupBy(c => c.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Id).ToArray());

        var result = new HashSet<int> { categoryId };
        var queue = new Queue<int>();
        queue.Enqueue(categoryId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!byParent.TryGetValue(current, out var children)) continue;

            foreach (var child in children)
                if (result.Add(child)) queue.Enqueue(child);
        }

        return result;
    }

    public async Task<CategoryDto> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var flat = await GetFlatAsync(ct);
        return flat.FirstOrDefault(c => c.Slug == slug)
            ?? throw NotFoundException.For("Kategori", slug);
    }
}
