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

    public ICollection<CartItem> Items { get; set; } = [];
}

public class CartItem
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }

    public Cart Cart { get; set; } = null!;
    public Product Product { get; set; } = null!;
}