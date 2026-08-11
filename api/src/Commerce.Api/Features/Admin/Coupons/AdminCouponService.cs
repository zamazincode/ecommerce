using Commerce.Api.Common.Exceptions;
using Commerce.Api.Common.Extensions;
using Commerce.Api.Persistence;
using Commerce.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Api.Features.Admin.Coupons;

public sealed class AdminCouponService(AppDbContext db)
{
    public async Task<IReadOnlyList<AdminCouponDto>> ListAsync(CancellationToken ct = default)
        => await db.Coupons.AsNoTracking()
            .OrderByDescending(c => c.ValidTo)
            .Select(c => new AdminCouponDto(
                c.Id, c.Code, c.Type, c.Value, c.MinCartTotal,
                c.ValidFrom, c.ValidTo, c.UsageLimit, c.UsedCount, c.IsActive))
            .ToListAsync(ct);

    public async Task<AdminCouponDto> CreateAsync(
        CreateCouponRequest request, CancellationToken ct = default)
    {
        // Sepet tarafı kuponu her zaman büyük harfle arıyor (CartService) —
        // aynı normalizasyon burada da uygulanmazsa kupon asla eşleşmez.
        var code = request.Code.Trim().ToUpperInvariant();

        if (await db.Coupons.AnyAsync(c => c.Code == code, ct))
            throw new ConflictException("Bu kupon kodu zaten kullanımda.");

        var coupon = new Coupon
        {
            Code = code,
            Type = request.Type,
            Value = request.Value,
            MinCartTotal = request.MinCartTotal,
            // AsUtc() ŞART: gövdeden gelen "saat dilimsiz" bir tarih Kind=Unspecified
            // üretir, Npgsql timestamptz'e onu yazmayı reddediyor (plan 2.4).
            ValidFrom = request.ValidFrom.AsUtc(),
            ValidTo = request.ValidTo.AsUtc(),
            UsageLimit = request.UsageLimit,
            IsActive = true
        };

        db.Coupons.Add(coupon);
        await db.SaveChangesAsync(ct);

        return ToDto(coupon);
    }

    public async Task<AdminCouponDto> SetActiveAsync(
        int id, bool isActive, CancellationToken ct = default)
    {
        var coupon = await db.Coupons.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw NotFoundException.For("Kupon", id);

        coupon.IsActive = isActive;
        await db.SaveChangesAsync(ct);

        return ToDto(coupon);
    }

    private static AdminCouponDto ToDto(Coupon c)
        => new(c.Id, c.Code, c.Type, c.Value, c.MinCartTotal,
               c.ValidFrom, c.ValidTo, c.UsageLimit, c.UsedCount, c.IsActive);
}
