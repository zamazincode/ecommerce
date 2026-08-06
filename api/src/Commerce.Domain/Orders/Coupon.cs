using Commerce.Domain.Common;

namespace Commerce.Domain.Orders;

public class Coupon
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public CouponType Type { get; set; }

    /// Percentage ise 10 = %10. FixedAmount ise 50 = 50₺.
    public decimal Value { get; set; }

    public decimal MinCartTotal { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public int? UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public bool IsActive { get; set; } = true;
}