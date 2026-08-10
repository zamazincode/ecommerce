using Commerce.Domain.Catalog;
using Commerce.Domain.Common;

namespace Commerce.Domain.Orders;

public class Cart : IAuditable
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public string? CouponCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// Sepet hatırlatma maili en son ne zaman gitti (Faz 9).
    /// ReminderSentAt <= UpdatedAt ise sepet o mailden sonra DEĞİŞMİŞ demektir,
    /// yeni bir hatırlatma hak edilmiştir (K6).
    public DateTime? ReminderSentAt { get; set; }

    public ICollection<CartItem> Items { get; set; } = [];
}

public class CartItem
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }

    /// Sepete eklendiği andaki efektif fiyat. SADECE "fiyat değişti" uyarısı için.
    /// Hesaplamada ASLA kullanılmaz — sepette fiyat dondurmak istismara açıktır
    /// (100₺'yken ekle, 500₺ olunca 100₺'den al).
    /// 0 = "bilinmiyor" (eski kayıt) → uyarı üretilmez.
    public decimal UnitPriceWhenAdded { get; set; }

    public Cart Cart { get; set; } = null!;
    public Product Product { get; set; } = null!;
}