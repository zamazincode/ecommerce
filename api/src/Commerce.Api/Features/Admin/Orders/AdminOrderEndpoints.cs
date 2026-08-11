using Commerce.Api.Common.Filters;
using Commerce.Api.Common.Results;
using Commerce.Api.Features.Admin.Orders.Dtos;
using Commerce.Api.Features.Orders.Dtos;

namespace Commerce.Api.Features.Admin.Orders;

public static class AdminOrderEndpoints
{
    // Sipariş DETAYI için ayrı bir uç yok: GET /api/orders/{orderNumber} admin'e
    // zaten açık (OrderService.EnsureOwnership isAdmin ile bypass ediyor).
    public static RouteGroupBuilder MapAdminOrderEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/orders", GetOrders)
             .WithSummary("Siparişleri filtreli/sayfalı listeler")
             .Produces<PagedResult<AdminOrderListDto>>()
             .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPatch("/orders/{orderNumber}/status", UpdateStatus)
             .WithValidation<UpdateOrderStatusRequest>()
             .WithSummary("Sipariş durumunu değiştirir (durum makinesi + stok iadesi dahil)")
             .Produces<OrderDetailDto>()
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<PagedResult<AdminOrderListDto>> GetOrders(
        [AsParameters] AdminOrderFilterRequest filter, AdminOrderService service, CancellationToken ct)
        => await service.SearchAsync(filter, ct);

    private static async Task<OrderDetailDto> UpdateStatus(
        string orderNumber, UpdateOrderStatusRequest body, AdminOrderService service, CancellationToken ct)
        => await service.UpdateStatusAsync(orderNumber, body.Status, ct);
}
