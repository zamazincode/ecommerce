using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace Commerce.Api.Features.Cart;

/// Redis'te saklanan misafir sepeti. Sadece ürün kimliği ve adet —
/// fiyat, ad, görsel her okumada veritabanından taze gelir.
public sealed record GuestCart
{
    public List<GuestCartItem> Items { get; init; } = [];
    public string? CouponCode { get; init; }
}

public sealed record GuestCartItem(int ProductId, int Quantity, decimal UnitPriceWhenAdded);

public interface IGuestCartStore
{
    Task<GuestCart> GetAsync(string guestId, CancellationToken ct = default);
    Task SaveAsync(string guestId, GuestCart cart, CancellationToken ct = default);
    Task RemoveAsync(string guestId, CancellationToken ct = default);
}

public sealed class DistributedGuestCartStore(
    IDistributedCache cache,
    ILogger<DistributedGuestCartStore> logger) : IGuestCartStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromDays(7);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string Key(string guestId) => $"cart:guest:{guestId}";

    public async Task<GuestCart> GetAsync(string guestId, CancellationToken ct = default)
    {
        var raw = await cache.GetStringAsync(Key(guestId), ct);
        if (string.IsNullOrEmpty(raw)) return new GuestCart();

        try
        {
            // "null" gövdesi JsonException FIRLATMAZ, doğrudan null döner —
            // ?? burada ZORUNLU (ölçüldü).
            return JsonSerializer.Deserialize<GuestCart>(raw, JsonOptions) ?? new GuestCart();
        }
        catch (JsonException ex)
        {
            // SÜRÜM UYUMLULUĞU: Modele alan eklediğinde Redis'te eski biçimde
            // kayıtlar kalır. Ama asıl senaryo bu try/catch'e bile düşmüyor —
            // eksik alan istisna atmadan sessizce 0 dolduruyor (GuestCartItem
            // pozitif kayıt/varsayılan). Burası sadece TAMAMEN bozuk JSON içindir.
            logger.LogWarning(ex, "Misafir sepeti çözümlenemedi, sıfırlanıyor. GuestId: {GuestId}", guestId);
            await RemoveAsync(guestId, ct);
            return new GuestCart();
        }
    }

    public async Task SaveAsync(string guestId, GuestCart cart, CancellationToken ct = default)
    {
        if (cart.Items.Count == 0 && cart.CouponCode is null)
        {
            await RemoveAsync(guestId, ct);
            return;
        }

        await cache.SetStringAsync(
            Key(guestId),
            JsonSerializer.Serialize(cart, JsonOptions),
            // Kayan süre: kullanıcı sepete her dokunduğunda 7 gün yeniden başlar.
            new DistributedCacheEntryOptions { SlidingExpiration = Ttl },
            ct);
    }

    public Task RemoveAsync(string guestId, CancellationToken ct = default)
        => cache.RemoveAsync(Key(guestId), ct);
}
