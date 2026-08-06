using Commerce.Domain.Common;

namespace Commerce.Domain.Orders;

public class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }

    public string Provider { get; set; } = null!;          // "iyzico", "fake"
    public string? ProviderTransactionId { get; set; }     // UNIQUE — idempotency
    public PaymentStatus Status { get; set; }
    public decimal Amount { get; set; }

    public string? RawResponse { get; set; }

    public DateTime CreatedAt { get; set; }

    public Order Order { get; set; } = null!;
}