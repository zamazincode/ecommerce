using Commerce.Domain.Common;

namespace Commerce.Domain.Orders;

public sealed class InvalidOrderStatusTransitionException(OrderStatus from, OrderStatus to)
    : DomainRuleException($"Sipariş durumu '{from}' → '{to}' geçişi geçersiz.")
{
    public OrderStatus From { get; } = from;
    public OrderStatus To { get; } = to;
}

/// Saf sınıf: veritabanı yok, saat yok, log yok.
/// Bu yüzden 20 satırlık testle tüm iş kuralını kilitleyebiliyoruz.
public static class OrderStatusTransition
{
    /// Diyagramda olmayan HER geçiş geçersizdir.
    /// Boş dizi = terminal durum, çıkışı yok.
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new()
    {
        [OrderStatus.Pending] = [OrderStatus.Paid, OrderStatus.Cancelled],
        [OrderStatus.Paid] = [OrderStatus.Preparing, OrderStatus.Cancelled, OrderStatus.Refunded],
        [OrderStatus.Preparing] = [OrderStatus.Shipped, OrderStatus.Cancelled],
        [OrderStatus.Shipped] = [OrderStatus.Delivered],
        [OrderStatus.Delivered] = [],
        [OrderStatus.Cancelled] = [],
        [OrderStatus.Refunded] = []
    };

    public static bool CanTransition(OrderStatus from, OrderStatus to)
        => Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static void EnsureCanTransition(OrderStatus from, OrderStatus to)
    {
        if (!CanTransition(from, to))
            throw new InvalidOrderStatusTransitionException(from, to);
    }

    /// Müşteri kendi siparişini yalnızca bu durumlarda iptal edebilir.
    /// Kargoya verildikten sonra iptal, kargo süreciyle uyumsuz olur.
    public static bool IsCancellableByCustomer(OrderStatus status)
        => status is OrderStatus.Pending or OrderStatus.Paid;

    /// Bu durumlara geçişte stok geri eklenmeli.
    public static bool RestoresStock(OrderStatus to)
        => to is OrderStatus.Cancelled or OrderStatus.Refunded;

    public static IReadOnlyList<OrderStatus> AllowedTargets(OrderStatus from)
        => Allowed.TryGetValue(from, out var targets) ? targets : [];
}
