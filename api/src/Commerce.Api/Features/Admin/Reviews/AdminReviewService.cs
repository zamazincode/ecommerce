using Commerce.Api.Common.Caching;
using Commerce.Api.Common.Exceptions;
using Commerce.Api.Common.Extensions;
using Commerce.Api.Common.Results;
using Commerce.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Commerce.Api.Features.Admin.Reviews;

/// Yorum OLUŞTURAN bir uç yok (PLAN.md'nin ürün yorumları maddesi Faz E3'e
/// bırakıldı — bkz. `docs/planlar/faz11-admin-api-plan.md` §0). Bu uçlar
/// yalnızca moderasyon altyapısını hazır tutuyor; üretimde Reviews tablosu
/// Faz E3'e kadar boş kalacak.
public sealed class AdminReviewService(AppDbContext db, HybridCache cache)
{
    public async Task<PagedResult<AdminReviewDto>> SearchAsync(
        AdminReviewFilterRequest filter, CancellationToken ct = default)
    {
        var query = db.Reviews.AsNoTracking()
            .WhereIf(filter.OnlyPending == true, r => !r.IsApproved)
            .WhereIf(filter.ProductId.HasValue, r => r.ProductId == filter.ProductId!.Value)
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id);

        return await query.Select(r => new AdminReviewDto(
                r.Id, r.ProductId, r.Product.Name, r.UserId,
                db.Users.Where(u => u.Id == r.UserId).Select(u => u.Email).FirstOrDefault(),
                r.Rating, r.Comment, r.IsApproved, r.CreatedAt))
            .ToPagedResultAsync(filter.ToPageRequest(), ct);
    }

    public async Task ApproveAsync(int id, CancellationToken ct = default)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw NotFoundException.For("Yorum", id);

        review.IsApproved = true;
        await db.SaveChangesAsync(ct);

        await cache.RemoveByTagAsync(CacheTags.Products, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw NotFoundException.For("Yorum", id);

        db.Reviews.Remove(review);
        await db.SaveChangesAsync(ct);

        await cache.RemoveByTagAsync(CacheTags.Products, ct);
    }
}
