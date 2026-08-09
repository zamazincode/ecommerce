using System.Security.Claims;
using Commerce.Api.Common.Extensions;
using Commerce.Api.Common.Filters;
using Commerce.Api.Features.Cart.Dtos;

namespace Commerce.Api.Features.Cart;

public static class CartEndpoints
{
    public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cart")
                       .WithTags("Cart")
                       // Misafir sepeti tamamen anonim yazılabiliyor (X-Guest-Id
                       // istemci üretimi) — GlobalLimiter'ın (200/dk) üstüne ikinci katman.
                       .RequireRateLimiting("cart");

        group.MapGet("/", GetCart)
             .WithSummary("Sepeti getirir (üye veya misafir)")
             .Produces<CartDto>()
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/items", AddItem)
             .WithValidation<AddCartItemRequest>()
             .WithSummary("Sepete ürün ekler; ürün zaten varsa adedi artırır")
             .Produces<CartDto>()
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status401Unauthorized)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/items/{productId:int}", UpdateItem)
             .WithValidation<UpdateCartItemRequest>()
             .WithSummary("Satır adedini değiştirir")
             .Produces<CartDto>()
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status401Unauthorized)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/items/{productId:int}", RemoveItem)
             .WithSummary("Ürünü sepetten çıkarır")
             .Produces<CartDto>()
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status401Unauthorized)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/", ClearCart)
             .WithSummary("Sepeti boşaltır")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/merge", Merge)
             .RequireAuthorization()
             .WithSummary("Giriş sonrası misafir sepetini üye sepetiyle birleştirir")
             .Produces<CartDto>()
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/coupon", ApplyCoupon)
             .WithValidation<ApplyCouponRequest>()
             .WithSummary("Kupon uygular")
             .Produces<CartDto>()
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapDelete("/coupon", RemoveCoupon)
             .WithSummary("Kuponu kaldırır")
             .Produces<CartDto>()
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<CartDto> GetCart(
        ClaimsPrincipal user, HttpRequest request, CartService service, CancellationToken ct)
        => await service.GetAsync(CartOwner.Resolve(user, request), ct);

    private static async Task<CartDto> AddItem(
        AddCartItemRequest body, ClaimsPrincipal user, HttpRequest request,
        CartService service, CancellationToken ct)
        => await service.AddItemAsync(CartOwner.Resolve(user, request), body, ct);

    private static async Task<CartDto> UpdateItem(
        int productId, UpdateCartItemRequest body, ClaimsPrincipal user, HttpRequest request,
        CartService service, CancellationToken ct)
        => await service.UpdateQuantityAsync(
            CartOwner.Resolve(user, request), productId, body.Quantity, ct);

    private static async Task<CartDto> RemoveItem(
        int productId, ClaimsPrincipal user, HttpRequest request,
        CartService service, CancellationToken ct)
        => await service.RemoveItemAsync(CartOwner.Resolve(user, request), productId, ct);

    private static async Task<IResult> ClearCart(
        ClaimsPrincipal user, HttpRequest request, CartService service, CancellationToken ct)
    {
        await service.ClearAsync(CartOwner.Resolve(user, request), ct);
        return TypedResults.NoContent();
    }

    private static async Task<CartDto> Merge(
        ClaimsPrincipal user, HttpRequest request, CartService service, CancellationToken ct)
    {
        // Sepet zaten token'dan çözülüyor; misafir kimliği de aynı GUID kuralına
        // tabi (CartOwner.Resolve ile aynı doğrulama, S14).
        var guestId = CartOwner.ParseGuestId(request);
        return await service.MergeAsync(user.GetUserId(), guestId, ct);
    }

    private static async Task<CartDto> ApplyCoupon(
        ApplyCouponRequest body, ClaimsPrincipal user, HttpRequest request,
        CartService service, CancellationToken ct)
        => await service.ApplyCouponAsync(CartOwner.Resolve(user, request), body.Code, ct);

    private static async Task<CartDto> RemoveCoupon(
        ClaimsPrincipal user, HttpRequest request, CartService service, CancellationToken ct)
        => await service.RemoveCouponAsync(CartOwner.Resolve(user, request), ct);
}
