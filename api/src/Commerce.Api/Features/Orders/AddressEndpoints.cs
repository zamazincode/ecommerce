using System.Security.Claims;
using Commerce.Api.Common.Extensions;
using Commerce.Api.Common.Filters;
using Commerce.Api.Features.Orders.Dtos;

namespace Commerce.Api.Features.Orders;

public static class AddressEndpoints
{
    public static IEndpointRouteBuilder MapAddressEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/addresses")
                       .WithTags("Addresses")
                       .RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, AddressService service, CancellationToken ct)
                => await service.GetAllAsync(user.GetUserId(), ct))
             .WithSummary("Kullanıcının adresleri")
             .Produces<IReadOnlyList<AddressDto>>();

        group.MapPost("/", async (
                SaveAddressRequest body, ClaimsPrincipal user,
                AddressService service, CancellationToken ct) =>
             {
                 var address = await service.CreateAsync(user.GetUserId(), body, ct);
                 return TypedResults.Created($"/api/addresses/{address.Id}", address);
             })
             .WithValidation<SaveAddressRequest>()
             .WithSummary("Yeni adres ekler")
             .Produces<AddressDto>(StatusCodes.Status201Created)
             .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:int}", async (
                int id, SaveAddressRequest body, ClaimsPrincipal user,
                AddressService service, CancellationToken ct)
                => await service.UpdateAsync(user.GetUserId(), id, body, ct))
             .WithValidation<SaveAddressRequest>()
             .WithSummary("Adresi günceller")
             .Produces<AddressDto>()
             .ProducesProblem(StatusCodes.Status403Forbidden)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:int}", async (
                int id, ClaimsPrincipal user, AddressService service, CancellationToken ct) =>
             {
                 await service.DeleteAsync(user.GetUserId(), id, ct);
                 return TypedResults.NoContent();
             })
             .WithSummary("Adresi siler")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status403Forbidden)
             .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
