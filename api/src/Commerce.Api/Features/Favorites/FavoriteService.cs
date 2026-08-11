using Commerce.Api.Common.Exceptions;
using Commerce.Api.Common.Images;
using Commerce.Api.Features.Catalog;
using Commerce.Api.Features.Catalog.Dtos;
using Commerce.Api.Persistence;
using Commerce.Domain.Reviews;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Api.Features.Favorites;

public sealed class FavoriteService(AppDbContext db, ProductImageUrls urls, TimeProvider clock)
{
    public async Task<IReadOnlyList<ProductListDto>> GetAllAsync(
        Guid userId, CancellationToken ct = default)
        => await db.Favorites.AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => f.Product)
            .Select(ProductService.ListProjection(urls))
            .ToListAsync(ct);

    /// Kart bileşenlerinde "bu ürün favoride mi" kontrolü için HAFİF uç —
    /// tam ürün verisini taşımaz, yalnızca id kümesi.
    public async Task<IReadOnlyList<int>> GetFavoritedIdsAsync(
        Guid userId, CancellationToken ct = default)
        => await db.Favorites.AsNoTracking()
            .Where(f => f.UserId == userId)
            .Select(f => f.ProductId)
            .ToListAsync(ct);

    /// İDEMPOTENT: zaten favorideyse sessizce başarı döner — istemcinin
    /// "önce kontrol et sonra ekle" yapmasına gerek yok.
    public async Task AddAsync(Guid userId, int productId, CancellationToken ct = default)
    {
        var productExists = await db.Products.AnyAsync(p => p.Id == productId, ct);
        if (!productExists) throw NotFoundException.For("Ürün", productId);

        var alreadyFavorited = await db.Favorites
            .AnyAsync(f => f.UserId == userId && f.ProductId == productId, ct);
        if (alreadyFavorited) return;

        db.Favorites.Add(new Favorite
        {
            UserId = userId,
            ProductId = productId,
            CreatedAt = clock.GetUtcNow().UtcDateTime
        });
        await db.SaveChangesAsync(ct);
    }

    /// Favoride değilse de sessizce başarı — "zaten yok" bir hata değil.
    public async Task RemoveAsync(Guid userId, int productId, CancellationToken ct = default)
        => await db.Favorites
            .Where(f => f.UserId == userId && f.ProductId == productId)
            .ExecuteDeleteAsync(ct);
}
