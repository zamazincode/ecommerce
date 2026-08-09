using Commerce.Domain.Orders;

namespace Commerce.Domain.Pricing;

public enum CouponRejectionReason
{
    None = 0,
    NotFound,
    Inactive,
    NotStarted,
    Expired,
    UsageLimitReached,
    MinimumCartTotalNotMet
}

public sealed record CouponValidationResult(
    bool IsValid,
    CouponRejectionReason Reason,
    string? Message)
{
    public static CouponValidationResult Ok() => new(true, CouponRejectionReason.None, null);

    public static CouponValidationResult Fail(CouponRejectionReason reason, string message)
        => new(false, reason, message);
}

public static class CouponValidator
{
    /// <param name="nowUtc">TimeProvider'dan gelir — DateTime.UtcNow değil.</param>
    public static CouponValidationResult Validate(
        Coupon? coupon, decimal subTotal, DateTime nowUtc)
    {
        if (coupon is null)
            return CouponValidationResult.Fail(
                CouponRejectionReason.NotFound, "Kupon bulunamadı.");

        if (!coupon.IsActive)
            return CouponValidationResult.Fail(
                CouponRejectionReason.Inactive, "Bu kupon artık geçerli değil.");

        if (nowUtc < coupon.ValidFrom)
            return CouponValidationResult.Fail(
                CouponRejectionReason.NotStarted, "Bu kupon henüz kullanıma açılmadı.");

        if (nowUtc >= coupon.ValidTo)
            return CouponValidationResult.Fail(
                CouponRejectionReason.Expired, "Kuponun süresi dolmuş.");

        if (coupon.UsageLimit is { } limit && coupon.UsedCount >= limit)
            return CouponValidationResult.Fail(
                CouponRejectionReason.UsageLimitReached, "Kupon kullanım limiti dolmuş.");

        if (subTotal < coupon.MinCartTotal)
            return CouponValidationResult.Fail(
                CouponRejectionReason.MinimumCartTotalNotMet,
                $"Bu kupon en az {coupon.MinCartTotal:N2} ₺ tutarındaki sepetlerde geçerli.");

        return CouponValidationResult.Ok();
    }
}
