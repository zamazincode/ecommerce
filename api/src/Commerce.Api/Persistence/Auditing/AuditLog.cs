namespace Commerce.Api.Persistence.Auditing;

/// Yalnızca ADMIN HTTP isteklerinden yapılan yazmalar kaydedilir (plan K2).
/// Bilerek Commerce.Domain'de değil: bu bir iş kuralı değil, altyapı konusu —
/// ApplicationUser'ın Commerce.Api'de yaşamasıyla aynı gerekçe.
public class AuditLog
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public string EntityType { get; set; } = null!;

    /// Added girdilerde SavingChanges anında PK henüz geçici (EF'in negatif
    /// geçici anahtarı) — gerçek değer SavedChangesAsync'teki fix-up'ta yazılır.
    public string? EntityId { get; set; }
    public string Action { get; set; } = null!;    // Created / Updated / Deleted
    public string? OldValues { get; set; }          // jsonb
    public string? NewValues { get; set; }          // jsonb
    public DateTime CreatedAt { get; set; }
}
