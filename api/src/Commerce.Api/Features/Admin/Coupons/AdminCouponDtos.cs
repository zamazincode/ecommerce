using Commerce.Domain.Common;

namespace Commerce.Api.Features.Admin.Coupons;

public sealed record CreateCouponRequest(
    string Code, CouponType Type, decimal Value, decimal MinCartTotal,
    DateTime ValidFrom, DateTime ValidTo, int? UsageLimit);

public sealed record UpdateCouponStatusRequest(bool IsActive);

public sealed record AdminCouponDto(
    int Id, string Code, CouponType Type, decimal Value, decimal MinCartTotal,
    DateTime ValidFrom, DateTime ValidTo, int? UsageLimit, int UsedCount, bool IsActive);
