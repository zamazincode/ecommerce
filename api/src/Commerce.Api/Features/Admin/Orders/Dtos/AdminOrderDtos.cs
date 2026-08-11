using Commerce.Api.Common.Results;
using Commerce.Domain.Common;

namespace Commerce.Api.Features.Admin.Orders.Dtos;

public sealed record AdminOrderFilterRequest
{
    // TÜMÜ NULLABLE — [AsParameters] non-nullable'ı zorunlu sayıyor (CLAUDE.md
    // tuzağı). DateOnly? kullanılıyor: query string'ten gelen DateTime hiçbir
    // biçimde Kind=Utc olmuyor ve Npgsql onu reddediyor (plan 2.4).
    public OrderStatus? Status { get; init; }
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
    public string? Q { get; init; }          // sipariş numarası ya da e-posta
    public int? Page { get; init; }
    public int? PageSize { get; init; }

    public PageRequest ToPageRequest() => new()
    {
        Page = Page ?? 1,
        PageSize = PageSize ?? PageRequest.DefaultPageSize
    };
}

public sealed record AdminOrderListDto(
    int Id, string OrderNumber, OrderStatus Status, decimal Total,
    string? CustomerEmail, string? CustomerName, int ItemCount, DateTime CreatedAt);

public sealed record UpdateOrderStatusRequest(OrderStatus Status);
