using Commerce.Domain.Common;
using Commerce.Domain.Orders;
using Commerce.Domain.Pricing;
using Shouldly;

namespace Commerce.UnitTests.Pricing;

public class CouponValidatorTests
{
    private static readonly DateTime Now = new(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc);

    private static Coupon ValidCoupon() => new()
    {
        Code = "TEST10",
        Type = CouponType.Percentage,
        Value = 10m,
        MinCartTotal = 100m,
        ValidFrom = Now.AddDays(-10),
        ValidTo = Now.AddDays(10),
        UsageLimit = 100,
        UsedCount = 5,
        IsActive = true
    };

    [Fact]
    public void Validate_WithValidCoupon_ReturnsSuccess()
    {
        var result = CouponValidator.Validate(ValidCoupon(), subTotal: 150m, Now);

        result.IsValid.ShouldBeTrue();
        result.Reason.ShouldBe(CouponRejectionReason.None);
    }

    [Fact]
    public void Validate_WithNullCoupon_ReturnsNotFound()
    {
        var result = CouponValidator.Validate(null, 150m, Now);

        result.IsValid.ShouldBeFalse();
        result.Reason.ShouldBe(CouponRejectionReason.NotFound);
    }

    [Fact]
    public void Validate_WhenInactive_ReturnsInactive()
    {
        var coupon = ValidCoupon();
        coupon.IsActive = false;

        var result = CouponValidator.Validate(coupon, 150m, Now);

        result.Reason.ShouldBe(CouponRejectionReason.Inactive);
    }

    [Fact]
    public void Validate_WhenNotYetStarted_ReturnsNotStarted()
    {
        var coupon = ValidCoupon();
        coupon.ValidFrom = Now.AddDays(1);

        var result = CouponValidator.Validate(coupon, 150m, Now);

        result.Reason.ShouldBe(CouponRejectionReason.NotStarted);
    }

    [Fact]
    public void Validate_WhenExpired_ReturnsExpired()
    {
        var coupon = ValidCoupon();
        coupon.ValidTo = Now.AddDays(-1);

        var result = CouponValidator.Validate(coupon, 150m, Now);

        result.Reason.ShouldBe(CouponRejectionReason.Expired);
    }

    [Fact]
    public void Validate_WhenUsageLimitReached_ReturnsUsageLimitReached()
    {
        var coupon = ValidCoupon();
        coupon.UsedCount = coupon.UsageLimit!.Value;

        var result = CouponValidator.Validate(coupon, 150m, Now);

        result.Reason.ShouldBe(CouponRejectionReason.UsageLimitReached);
    }

    [Fact]
    public void Validate_WhenUsageLimitIsNull_TreatsAsUnlimited()
    {
        var coupon = ValidCoupon();
        coupon.UsageLimit = null;
        coupon.UsedCount = 999_999;

        var result = CouponValidator.Validate(coupon, 150m, Now);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenSubTotalBelowMinimum_ReturnsMinimumNotMet()
    {
        var result = CouponValidator.Validate(ValidCoupon(), subTotal: 99.99m, Now);

        result.Reason.ShouldBe(CouponRejectionReason.MinimumCartTotalNotMet);
        result.Message.ShouldNotBeNull().ShouldContain("100");
    }

    [Fact]
    public void Validate_WhenSubTotalExactlyMinimum_IsValid()
    {
        var coupon = ValidCoupon();

        var result = CouponValidator.Validate(coupon, subTotal: coupon.MinCartTotal, Now);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_AtExactValidFromInstant_IsValid()
    {
        var coupon = ValidCoupon();
        coupon.ValidFrom = Now;

        var result = CouponValidator.Validate(coupon, 150m, Now);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(-1, true)]     // nowUtc, ValidTo'dan 1sn ÖNCE → hâlâ geçerli
    [InlineData(0, false)]     // nowUtc == ValidTo → Expired (dahil değil)
    [InlineData(1, false)]     // nowUtc, ValidTo'dan 1sn SONRA → Expired
    public void Validate_ValidToBoundary_IsExclusive(int secondsOffsetFromValidTo, bool expectedValid)
    {
        var coupon = ValidCoupon();     // ValidTo = Now.AddDays(10) sabit kalır
        var nowUtc = coupon.ValidTo.AddSeconds(secondsOffsetFromValidTo);

        var result = CouponValidator.Validate(coupon, 150m, nowUtc);

        result.IsValid.ShouldBe(expectedValid);
        if (!expectedValid)
            result.Reason.ShouldBe(CouponRejectionReason.Expired);
    }
}
