using System.Security.Claims;
using Commerce.Api.Common.Extensions;
using Commerce.Api.Features.Catalog.Dtos;

namespace Commerce.Api.Features.Favorites;

public static class FavoriteEndpoints
{
    public static IEndpointRouteBuilder MapFavoriteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/favorites")
                       .WithTags("Favorites")
                       .RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, FavoriteService service, CancellationToken ct)
                => await service.GetAllAsync(user.GetUserId(), ct))
             .WithSummary("Favori ürünler")
             .Produces<IReadOnlyList<ProductListDto>>();

        group.MapGet("/ids", async (ClaimsPrincipal user, FavoriteService service, CancellationToken ct)
                => await service.GetFavoritedIdsAsync(user.GetUserId(), ct))
             .WithSummary("Favorilenen ürün ID'leri (hafif, kart bileşenleri için)")
             .Produces<IReadOnlyList<int>>();

        group.MapPost("/{productId:int}", async (
                int productId, ClaimsPrincipal user, FavoriteService service, CancellationToken ct) =>
             {
                 await service.AddAsync(user.GetUserId(), productId, ct);
                 return TypedResults.NoContent();
             })
             .WithSummary("Ürünü favorilere ekler (idempotent)")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{productId:int}", async (
                int productId, ClaimsPrincipal user, FavoriteService service, CancellationToken ct) =>
             {
                 await service.RemoveAsync(user.GetUserId(), productId, ct);
                 return TypedResults.NoContent();
             })
             .WithSummary("Ürünü favorilerden çıkarır")
             .Produces(StatusCodes.Status204NoContent);

        return app;
    }
}
