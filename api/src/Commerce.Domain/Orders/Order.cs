using Commerce.Domain.Common;

namespace Commerce.Domain.Orders;

public class Order : IAuditable
{
    public int Id { get; set; }
    public Guid UserId { get; set; }

    /// Kullanıcıya gösterilen numara. Id'yi ASLA dışarı verme —
    /// kaç sipariş aldığını ifşa eder.
    public string OrderNumber { get; set; } = null!;

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public decimal SubTotal { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }
    public string? CouponCode { get; set; }

    /// Sipariş anında KOPYALANIR. Kullanıcı adresini silse bile
    /// eski siparişin adresi kaybolmaz.
    public OrderAddress ShippingAddress { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// Idempotency: onay maili iki kez gitmesin (Faz 9).
    public DateTime? ConfirmationEmailSentAt { get; set; }

    /// Idempotency: kargo bildirimi iki kez gitmesin (Faz 9).
    /// ConfirmationEmailSentAt ile aynı desen — o Faz 1'de eklenmişti.
    public DateTime? ShippedEmailSentAt { get; set; }

    /// İstemcinin ürettiği tekil anahtar. Aynı anahtarla ikinci istek gelirse
    /// yeni sipariş oluşturmaz, mevcut olanı döndürür.
    public string? IdempotencyKey { get; set; }

    /// Müşteri notu (kapıya bırak, zile basma…). Sipariş oluşurken alınır,
    /// sonradan değiştirilemez.
    public string? Note { get; set; }

    public ICollection<OrderItem> Items { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
}

/// Owned type — ayrı tablo değil, Orders tablosunda kolonlar olarak durur.
public class OrderAddress
{
    public string FullName { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string City { get; set; } = null!;
    public string District { get; set; } = null!;
    public string FullAddress { get; set; } = null!;
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }

    /// Sadece FK — navigation property YOK.
    /// Ürün silinse bile satır anlamını korumalı; o yüzden aşağıdaki
    /// snapshot alanları var.
    public int ProductId { get; set; }

    public string ProductNameSnapshot { get; set; } = null!;
    public string ProductSlugSnapshot { get; set; } = null!;
    public decimal UnitPriceSnapshot { get; set; }
    public int Quantity { get; set; }

    public Order Order { get; set; } = null!;

    public decimal LineTotal => UnitPriceSnapshot * Quantity;
}
