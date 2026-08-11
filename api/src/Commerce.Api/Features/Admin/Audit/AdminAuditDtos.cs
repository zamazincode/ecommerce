using Commerce.Api.Common.Results;

namespace Commerce.Api.Features.Admin.Audit;

public sealed record AuditLogFilterRequest
{
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    public string? Action { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }

    public PageRequest ToPageRequest() => new()
    {
        Page = Page ?? 1,
        PageSize = PageSize ?? PageRequest.DefaultPageSize
    };
}

/// OldValues/NewValues STRING olarak döner (jsonb kolonu string'e map'li) —
/// istemci JSON.parse eder. Burada object'e çevirmek gereksiz iki kez
/// ayrıştırma demek olurdu.
public sealed record AuditLogDto(
    long Id, Guid? UserId, string? UserEmail, string EntityType, string? EntityId,
    string Action, string? OldValues, string? NewValues, DateTime CreatedAt);
