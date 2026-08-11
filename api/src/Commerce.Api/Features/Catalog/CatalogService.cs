using Commerce.Api.Common.Caching;
using Commerce.Api.Common.Exceptions;
using Commerce.Api.Common.Images;
using Commerce.Api.Features.Catalog.Dtos;
using Commerce.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Commerce.Api.Features.Catalog;

public sealed class CatalogService(
    AppDbContext db, HybridCache cache, ProductService products, ProductImageUrls urls)
{
    public async Task<AuthorDetailDto> GetAuthorBySlugAsync(
        string slug, CancellationToken ct = default)
        => await db.Authors
            .AsNoTracking()
            .Where(a => a.Slug == slug)
            .Select(a => new AuthorDetailDto(a.Id, a.Name, a.Slug, a.Bio))
            .FirstOrDefaultAsync(ct)
           ?? throw NotFoundException.For("Yazar", slug);

    public async Task<IReadOnlyList<PublisherBriefDto>> GetPublishersAsync(
        CancellationToken ct = default)
        => await db.Publishers
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new PublisherBriefDto(p.Id, p.Name, p.Slug))
            .ToListAsync(ct);

    public async Task<HomeDto> GetHomeAsync(CancellationToken ct = default)
        => await cache.GetOrCreateAsync(
            CacheKeys.Home,
            this,
            static async (self, token) => await self.LoadHomeAsync(token),
            new HybridCacheEntryOptions { Expiration = CacheDurations.Home },
            tags: [CacheTags.Products],
            cancellationToken: ct);

    private async Task<HomeDto> LoadHomeAsync(CancellationToken ct)
    {
        // Çok satanlar: sipariş satırlarındaki toplam adede göre.
        // Henüz sipariş yoksa boş döner, aşağıda yenilere düşüyoruz.
        var bestsellerIds = await db.OrderItems
            .AsNoTracking()
            .GroupBy(oi => oi.ProductId)
            .OrderByDescending(g => g.Sum(x => x.Quantity))
            .Select(g => g.Key)
            .Take(12)
            .ToListAsync(ct);

        // SIRA ÖNEMLİ: filtrele → sırala → sayfala → EN SONDA projekte et.
        // Projeksiyondan sonra OrderBy yazarsan EF çeviremez.
        var bestsellers = bestsellerIds.Count == 0
            ? []
            : await products.ActiveProducts()
                .Where(p => bestsellerIds.Contains(p.Id))
                .Select(ProductService.ListProjection(urls))
                .ToListAsync(ct);

        var newArrivals = await products.ActiveProducts()
            .OrderByDescending(p => p.Id)
            .Take(12)
            .Select(ProductService.ListProjection(urls))
            .ToListAsync(ct);

        // EffectivePrice ENTITY üzerinde kullanılamaz (veritabanında yok).
        // Aynı hesabı açıkça yazıyoruz.
        var discounted = await products.ActiveProducts()
            .Where(p => p.DiscountedPrice != null)
            .OrderBy(p => p.DiscountedPrice ?? p.Price)
            .Take(12)
            .Select(ProductService.ListProjection(urls))
            .ToListAsync(ct);

        return new HomeDto(
            bestsellers.Count > 0 ? bestsellers : newArrivals,
            newArrivals,
            discounted);
    }
}
