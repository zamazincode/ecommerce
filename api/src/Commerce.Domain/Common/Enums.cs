namespace Commerce.Domain.Common;

public enum OrderStatus
{
    Pending = 0,
    Paid = 1,
    Preparing = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5,
    Refunded = 6
}

public enum PaymentStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Refunded = 3
}

public enum CouponType
{
    Percentage = 0,
    FixedAmount = 1
}

public enum BookBinding
{
    Unknown = 0,
    Paperback = 1,   // Karton kapak
    Hardcover = 2,   // Ciltli
    Ebook = 3
}