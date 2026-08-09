namespace Commerce.Api.Features.Cart.Dtos;

public sealed record CartItemDto(
    int ProductId,
    string Name,
    string Slug,
    string? ImageUrl,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    int AvailableStock,
    bool PriceChanged);

public sealed record CartDto(
    IReadOnlyList<CartItemDto> Items,
    string? CouponCode,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal ShippingCost,
    decimal Total,
    // Bedava kargoya kaç lira kaldı. 0 ise kargo zaten bedava.
    decimal FreeShippingRemaining,
    IReadOnlyList<string> Warnings)
{
    public int TotalQuantity => Items.Sum(i => i.Quantity);
}

public sealed record AddCartItemRequest(int ProductId, int Quantity);
public sealed record UpdateCartItemRequest(int Quantity);
public sealed record ApplyCouponRequest(string Code);

/// Sepetin DEPODAKİ hâli: kırpma yok, uyarı yok, fiyat tazeleme yok.
/// Faz 7 sipariş oluştururken kullanıcının GERÇEKTEN istediği adedi görmek
/// zorunda (CartDto adedi stoğa kırpar — plan ölçüm 2.3).
public sealed record CartRawLine(int ProductId, int Quantity, decimal UnitPriceWhenAdded);
public sealed record RawCart(IReadOnlyList<CartRawLine> Items, string? CouponCode);
