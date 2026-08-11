using Commerce.Api.Features.Admin.Audit;
using Commerce.Api.Features.Admin.Categories;
using Commerce.Api.Features.Admin.Coupons;
using Commerce.Api.Features.Admin.Images;
using Commerce.Api.Features.Admin.Orders;
using Commerce.Api.Features.Admin.Products;
using Commerce.Api.Features.Admin.Reports;
using Commerce.Api.Features.Admin.Reviews;
using Commerce.Api.Features.Auth;

namespace Commerce.Api.Features.Admin;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // TEK YETKİLENDİRME NOKTASI. Yeni bir admin ucu eklemek isteyen kişi
        // zaten bu grubun içine yazmak zorunda — RequireAuthorization unutulamaz.
        var group = app.MapGroup("/api/admin")
                       .WithTags("Admin")
                       .RequireAuthorization(AuthPolicies.AdminOnly);

        group.MapAdminProductEndpoints();
        group.MapAdminCategoryEndpoints();
        group.MapAdminOrderEndpoints();
        group.MapAdminCouponEndpoints();
        group.MapAdminReportEndpoints();
        group.MapAdminAuditEndpoints();
        group.MapAdminReviewEndpoints();
        group.MapAdminImageEndpoints();       // Faz 10 — dosyası taşınmadı, yalnızca grubu devraldı

        return app;
    }
}
