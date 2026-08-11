using Commerce.Api.Common.Results;

namespace Commerce.Api.Features.Admin.Audit;

public static class AdminAuditEndpoints
{
    public static RouteGroupBuilder MapAdminAuditEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/audit-logs", GetAuditLogs)
             .WithSummary("Denetim kaydını filtreli/sayfalı listeler")
             .Produces<PagedResult<AuditLogDto>>();

        return group;
    }

    private static async Task<PagedResult<AuditLogDto>> GetAuditLogs(
        [AsParameters] AuditLogFilterRequest filter, AdminAuditService service, CancellationToken ct)
        => await service.SearchAsync(filter, ct);
}
