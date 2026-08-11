using Commerce.Api.Common.Filters;

namespace Commerce.Api.Features.Admin.Coupons;

public static class AdminCouponEndpoints
{
    public static RouteGroupBuilder MapAdminCouponEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/coupons", GetCoupons)
             .WithSummary("Kuponları kullanım sayaçlarıyla listeler")
             .Produces<IReadOnlyList<AdminCouponDto>>();

        group.MapPost("/coupons", CreateCoupon)
             .WithValidation<CreateCouponRequest>()
             .WithSummary("Yeni kupon tanımlar (kod büyük harfe çevrilir)")
             .Produces<AdminCouponDto>(StatusCodes.Status201Created)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/coupons/{id:int}", SetCouponStatus)
             .WithValidation<UpdateCouponStatusRequest>()
             .WithSummary("Kuponu aktif/pasif yapar")
             .Produces<AdminCouponDto>()
             .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IReadOnlyList<AdminCouponDto>> GetCoupons(
        AdminCouponService service, CancellationToken ct)
        => await service.ListAsync(ct);

    private static async Task<IResult> CreateCoupon(
        CreateCouponRequest body, AdminCouponService service, CancellationToken ct)
    {
        var coupon = await service.CreateAsync(body, ct);
        return TypedResults.Created($"/api/admin/coupons/{coupon.Id}", coupon);
    }

    private static async Task<AdminCouponDto> SetCouponStatus(
        int id, UpdateCouponStatusRequest body, AdminCouponService service, CancellationToken ct)
        => await service.SetActiveAsync(id, body.IsActive, ct);
}
