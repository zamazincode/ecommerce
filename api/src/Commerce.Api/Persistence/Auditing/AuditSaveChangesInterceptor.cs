using System.Text.Json;
using Commerce.Api.Common.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Commerce.Api.Persistence.Auditing;

/// SCOPED kaydedilir: `_pending` listesi DbContext'e özgü durum tutuyor —
/// singleton olsaydı eşzamanlı iki isteğin denetim satırları birbirine karışırdı
/// (plan K2).
public sealed class AuditSaveChangesInterceptor(
    IHttpContextAccessor httpContextAccessor, TimeProvider clock) : SaveChangesInterceptor
{
    /// Added girdilerin PK'sı SavingChanges anında henüz geçici (ölçüldü, plan
    /// 2.5/A: -2147482647 gibi bir EF geçici anahtarı) — gerçek Id,
    /// SavedChangesAsync'te (SaveChanges'ten SONRA) fix-up ile yazılır.
    private readonly List<(AuditLog Log, EntityEntry Entry)> _pending = [];

    /// Fix-up'ın attığı ek SaveChanges kendini yeniden denetlemesin diye.
    private bool _fixingUp;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (!_fixingUp && eventData.Context is { } context)
            WriteAuditEntries(context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        if (_pending.Count > 0 && eventData.Context is { } context)
        {
            foreach (var (log, entry) in _pending)
                log.EntityId = KeyOf(entry);

            _pending.Clear();

            // TEK ek SaveChanges — _fixingUp bayrağı olmadan bu çağrı
            // SavingChangesAsync'i tekrar tetikler ve sonsuz döngü oluşur.
            _fixingUp = true;
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                _fixingUp = false;
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        // SaveChanges patladıysa denetim satırı da yazılmadı (aynı transaction,
        // ölçüldü — plan 2.5/E). Bekleyen fix-up listesini de temizle.
        _pending.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void WriteAuditEntries(DbContext context)
    {
        // NE ZAMAN yazılır: HttpContext var VE kullanıcı doğrulanmış VE Admin
        // rolünde. import/seed/Hangfire job'ı/müşteri işlemleri hiç denetlenmez.
        var user = httpContextAccessor.HttpContext?.User;
        if (user is not { Identity.IsAuthenticated: true } || !user.IsAdmin())
            return;

        var userId = user.GetUserIdOrNull();
        var now = clock.GetUtcNow().UtcDateTime;

        // ÖNCE LİSTEYE AL: aşağıdaki context.Add(log) ChangeTracker'ı değiştiriyor.
        // Canlı bir enumerator'ı enumere ederken değiştirmek "Collection was
        // modified; enumeration operation may not execute" fırlatıyor (ölçüldü —
        // gerçek bir admin isteğinde HER YAZMA 500 veriyordu).
        var entries = context.ChangeTracker.Entries().ToList();

        foreach (var entry in entries)
        {
            if (entry.Entity is AuditLog) continue;
            if (!AuditedEntities.Types.Contains(entry.Metadata.ClrType.Name)) continue;
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var action = entry.State switch
            {
                EntityState.Added => "Created",
                EntityState.Deleted => "Deleted",
                _ => "Updated"
            };

            var log = new AuditLog
            {
                UserId = userId,
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = entry.State == EntityState.Added ? null : KeyOf(entry),
                Action = action,
                OldValues = entry.State == EntityState.Added ? null : Serialize(entry, old: true),
                NewValues = entry.State == EntityState.Deleted ? null : Serialize(entry, old: false),
                CreatedAt = now
            };

            context.Add(log);

            if (entry.State == EntityState.Added)
                _pending.Add((log, entry));
        }
    }

    /// Bileşik anahtarlar da çalışsın diye PK property'lerini virgülle birleştirir.
    private static string KeyOf(EntityEntry entry)
        => string.Join(",", entry.Properties
            .Where(p => p.Metadata.IsPrimaryKey())
            .Select(p => p.CurrentValue?.ToString() ?? string.Empty));

    private static string? Serialize(EntityEntry entry, bool old)
    {
        var values = new Dictionary<string, object?>();

        foreach (var prop in entry.Properties)
        {
            if (prop.Metadata.IsPrimaryKey()) continue;
            if (AuditedEntities.SkippedProperties.Contains(prop.Metadata.Name)) continue;

            // Modified'da SADECE gerçekten değişen alanlar — denetim satırı
            // güncellenen her satırda tüm kolonları değil, farkı taşısın (S2).
            if (entry.State == EntityState.Modified && !prop.IsModified) continue;

            values[prop.Metadata.Name] = old ? prop.OriginalValue : prop.CurrentValue;
        }

        return values.Count == 0 ? null : JsonSerializer.Serialize(values);
    }
}
