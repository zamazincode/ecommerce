using Commerce.Api.Common.Extensions;
using Commerce.Api.Common.Results;
using Commerce.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Api.Features.Admin.Audit;

public sealed class AdminAuditService(AppDbContext db)
{
    public async Task<PagedResult<AuditLogDto>> SearchAsync(
        AuditLogFilterRequest filter, CancellationToken ct = default)
    {
        var query = db.AuditLogs.AsNoTracking()
            .WhereIf(!string.IsNullOrWhiteSpace(filter.EntityType), a => a.EntityType == filter.EntityType)
            .WhereIf(!string.IsNullOrWhiteSpace(filter.EntityId), a => a.EntityId == filter.EntityId)
            .WhereIf(!string.IsNullOrWhiteSpace(filter.Action), a => a.Action == filter.Action)
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id);

        return await query.Select(a => new AuditLogDto(
                a.Id, a.UserId,
                db.Users.Where(u => u.Id == a.UserId).Select(u => u.Email).FirstOrDefault(),
                a.EntityType, a.EntityId, a.Action, a.OldValues, a.NewValues, a.CreatedAt))
            .ToPagedResultAsync(filter.ToPageRequest(), ct);
    }
}
