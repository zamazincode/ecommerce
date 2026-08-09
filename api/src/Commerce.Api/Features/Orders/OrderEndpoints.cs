using System.Security.Claims;
using Commerce.Api.Common.Extensions;
using Commerce.Api.Common.Filters;
using Commerce.Api.Common.Results;
using Commerce.Api.Features.Orders.Dtos;

namespace Commerce.Api.Features.Orders;

public static class OrderEndpoints
{
    public const string IdempotencyHeader = "Idempotency-Key";

    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        // Yetkilendirme GRUP seviyesinde. Yeni endpoint eklerken
        // RequireAuthorization yazmayı unutmak imkânsız hâle geliyor.
        var group = app.MapGroup("/api/orders")
                       .WithTags("Orders")
                       .RequireAuthorization()
                       .RequireRateLimiting("orders");

        group.MapPost("/", CreateOrder)
             .WithValidation<CreateOrderRequest>()
             .WithSummary("Sepetten sipariş oluşturur")
             .WithDescription(
                 "Idempotency-Key başlığı gönderilirse aynı anahtarla ikinci istek " +
                 "yeni sipariş oluşturmaz, mevcut siparişi döndürür.")
             .Produces<OrderDetailDto>(StatusCodes.Status201Created)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status403Forbidden)
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", GetMyOrders)
             .WithSummary("Kullanıcının siparişleri (sayfalı)")
             .Produces<PagedResult<OrderListDto>>();

        group.MapGet("/{orderNumber}", GetOrder)
             .WithSummary("Sipariş detayı")
             .Produces<OrderDetailDto>()
             .ProducesProblem(StatusCodes.Status403Forbidden)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{orderNumber}/cancel", CancelOrder)
             .WithSummary("Siparişi iptal eder (yalnızca Pending ve Paid durumunda)")
             .Produces<OrderDetailDto>()
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status403Forbidden)
             .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateOrder(
        CreateOrderRequest body,
        ClaimsPrincipal user,
        HttpRequest request,
        OrderService service,
        CancellationToken ct)
    {
        var idempotencyKey = request.Headers[IdempotencyHeader].ToString();

        var order = await service.CreateAsync(
            user.GetUserId(), body,
            string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey,
            ct);

        return TypedResults.Created($"/api/orders/{order.OrderNumber}", order);
    }

    private static async Task<PagedResult<OrderListDto>> GetMyOrders(
        [AsParameters] OrderListRequest query,
        ClaimsPrincipal user,
        OrderService service,
        CancellationToken ct)
        => await service.GetMyOrdersAsync(user.GetUserId(), query.ToPageRequest(), ct);

    private static async Task<OrderDetailDto> GetOrder(
        string orderNumber, ClaimsPrincipal user, OrderService service, CancellationToken ct)
        => await service.GetByNumberAsync(user.GetUserId(), orderNumber, user.IsAdmin(), ct);

    private static async Task<OrderDetailDto> CancelOrder(
        string orderNumber, ClaimsPrincipal user, OrderService service, CancellationToken ct)
        => await service.CancelAsync(user.GetUserId(), orderNumber, user.IsAdmin(), ct);
}
