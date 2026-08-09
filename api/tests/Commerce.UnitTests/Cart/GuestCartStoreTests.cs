using Commerce.Api.Features.Cart;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Commerce.UnitTests.Cart;

/// Gerçek MemoryDistributedCache ile — sahte/mock değil. İkisi de shared
/// framework'te geliyor (Microsoft.AspNetCore.App), yeni paket gerekmiyor.
public class GuestCartStoreTests
{
    private static DistributedGuestCartStore CreateStore()
    {
        var cache = new ServiceCollection()
            .AddDistributedMemoryCache()
            .BuildServiceProvider()
            .GetRequiredService<IDistributedCache>();

        return new DistributedGuestCartStore(cache, NullLogger<DistributedGuestCartStore>.Instance);
    }

    [Fact]
    public async Task SaveThenGet_RoundTripsItemsAndCoupon()
    {
        var store = CreateStore();
        var guestId = Guid.NewGuid().ToString();
        var cart = new GuestCart
        {
            Items = [new GuestCartItem(7, 3, 19.99m), new GuestCartItem(8, 1, 5m)],
            CouponCode = "ON10"
        };

        await store.SaveAsync(guestId, cart, TestContext.Current.CancellationToken);
        var roundtripped = await store.GetAsync(guestId, TestContext.Current.CancellationToken);

        roundtripped.Items.Count.ShouldBe(2);
        roundtripped.Items[0].ShouldBe(new GuestCartItem(7, 3, 19.99m));
        roundtripped.CouponCode.ShouldBe("ON10");
    }

    [Fact]
    public async Task Get_WhenKeyMissing_ReturnsEmptyCart()
    {
        var store = CreateStore();

        var cart = await store.GetAsync(Guid.NewGuid().ToString(), TestContext.Current.CancellationToken);

        cart.Items.ShouldBeEmpty();
        cart.CouponCode.ShouldBeNull();
    }

    [Fact]
    public async Task Save_WhenCartIsEmpty_RemovesKey()
    {
        var cache = new ServiceCollection()
            .AddDistributedMemoryCache()
            .BuildServiceProvider()
            .GetRequiredService<IDistributedCache>();
        var store = new DistributedGuestCartStore(cache, NullLogger<DistributedGuestCartStore>.Instance);
        var guestId = Guid.NewGuid().ToString();

        // Önce bir şey yaz, sonra boşalt.
        await store.SaveAsync(guestId, new GuestCart { Items = [new GuestCartItem(1, 1, 10m)] },
            TestContext.Current.CancellationToken);
        await store.SaveAsync(guestId, new GuestCart(), TestContext.Current.CancellationToken);

        var raw = await cache.GetStringAsync($"cart:guest:{guestId}", TestContext.Current.CancellationToken);
        raw.ShouldBeNull();
    }

    [Fact]
    public async Task Get_WhenStoredJsonIsCorrupt_ReturnsEmptyCartAndClearsKey()
    {
        var cache = new ServiceCollection()
            .AddDistributedMemoryCache()
            .BuildServiceProvider()
            .GetRequiredService<IDistributedCache>();
        var store = new DistributedGuestCartStore(cache, NullLogger<DistributedGuestCartStore>.Instance);
        var guestId = Guid.NewGuid().ToString();
        var key = $"cart:guest:{guestId}";

        await cache.SetStringAsync(key, "{bozuk", TestContext.Current.CancellationToken);

        var cart = await store.GetAsync(guestId, TestContext.Current.CancellationToken);

        cart.Items.ShouldBeEmpty();
        (await cache.GetStringAsync(key, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Get_WhenStoredJsonLacksPriceField_DefaultsToZeroWithoutThrowing()
    {
        var cache = new ServiceCollection()
            .AddDistributedMemoryCache()
            .BuildServiceProvider()
            .GetRequiredService<IDistributedCache>();
        var store = new DistributedGuestCartStore(cache, NullLogger<DistributedGuestCartStore>.Instance);
        var guestId = Guid.NewGuid().ToString();
        var key = $"cart:guest:{guestId}";

        // Eski sürümden kalma kayıt: unitPriceWhenAdded alanı yok.
        // Bu senaryo JsonException FIRLATMIYOR — sessizce 0 dolduruyor (ölçüm 2.8).
        await cache.SetStringAsync(key, """{"items":[{"productId":7,"quantity":3}]}""",
            TestContext.Current.CancellationToken);

        var cart = await store.GetAsync(guestId, TestContext.Current.CancellationToken);

        cart.Items.Count.ShouldBe(1);
        cart.Items[0].UnitPriceWhenAdded.ShouldBe(0m);
    }
}
