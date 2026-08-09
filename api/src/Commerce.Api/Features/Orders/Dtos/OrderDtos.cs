using Commerce.Api.Common.Results;
using Commerce.Domain.Common;

namespace Commerce.Api.Features.Orders.Dtos;

/// ExpectedTotal: istemcinin sepette GÖRDÜĞÜ toplam. Fiyat kaynağı DEĞİL —
/// yalnızca karşılaştırılır; tutmazsa 409 döner ve sipariş oluşmaz (K7).
public sealed record CreateOrderRequest(int AddressId, string? Note, decimal? ExpectedTotal);

public sealed record OrderListDto(
    string OrderNumber,
    OrderStatus Status,
    decimal Total,
    int TotalQuantity,
    DateTime CreatedAt);

public sealed record OrderDetailDto(
    string OrderNumber,
    OrderStatus Status,
    decimal SubTotal,
    decimal ShippingCost,
    decimal DiscountAmount,
    decimal Total,
    string? CouponCode,
    string? Note,
    OrderAddressDto ShippingAddress,
    IReadOnlyList<OrderItemDto> Items,
    IReadOnlyList<OrderStatus> AllowedNextStatuses,
    bool CanBeCancelled,
    DateTime CreatedAt);

public sealed record OrderItemDto(
    int ProductId, string ProductName, string ProductSlug,
    decimal UnitPrice, int Quantity, decimal LineTotal);

public sealed record OrderAddressDto(
    string FullName, string Phone, string City, string District, string FullAddress);

/// [AsParameters] non-nullable property'leri ZORUNLU sayar (CLAUDE.md tuzağı,
/// ölçüm 2.5): PageRequest'i doğrudan bağlarsak query string'siz istek 400 döner.
public sealed record OrderListRequest
{
    public int? Page { get; init; }
    public int? PageSize { get; init; }

    public PageRequest ToPageRequest() => new()
    {
        Page = Page ?? 1,
        PageSize = PageSize ?? PageRequest.DefaultPageSize
    };
}
