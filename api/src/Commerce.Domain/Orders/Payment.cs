using Commerce.Domain.Common;

namespace Commerce.Domain.Orders;

public class Payment : IAuditable          // Order/Cart/Product ile aynı işaretleyici
{
    public int Id { get; set; }
    public int OrderId { get; set; }

    public string Provider { get; set; } = null!;          // "iyzico", "fake"
    public string? ProviderTransactionId { get; set; }     // UNIQUE — idempotency

    /// Ödeme BAŞLATILDIĞINDA sağlayıcının verdiği referans (iyzico'da token).
    /// Callback bu değerle geliyor; ödeme kaydını bununla buluyoruz.
    /// ProviderTransactionId ise ödeme TAMAMLANINCA doluyor.
    public string? ProviderReference { get; set; }

    public PaymentStatus Status { get; set; }
    public decimal Amount { get; set; }

    public string? RawResponse { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Order Order { get; set; } = null!;
}