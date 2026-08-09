using System.Net;
using System.Net.Http.Json;
using Commerce.Api.Features.Orders;
using Commerce.Api.Features.Orders.Dtos;
using Commerce.Domain.Users;
using Commerce.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Commerce.IntegrationTests.Orders;

public class AddressEndpointTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static object BuildPayload(
        string title = "Ev", string fullName = "Ali Veli", string phone = "5551112233",
        string city = "İstanbul", string district = "Kadıköy",
        string fullAddress = "Moda Caddesi No 1 Daire 5", bool isDefault = false)
        => new { title, fullName, phone, city, district, fullAddress, isDefault };

    private async Task<AddressDto> CreateAddressAsync(bool isDefault = false, string title = "Ev")
    {
        var response = await Client.PostAsJsonAsync(
            "/api/addresses", BuildPayload(title: title, isDefault: isDefault), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<AddressDto>(Ct))!;
    }

    [Fact]
    public async Task Create_FirstAddress_BecomesDefault()
    {
        await AuthenticateAsync();

        // isDefault: false gönderiliyor ama ilk adres OTOMATİK varsayılan olur.
        var response = await Client.PostAsJsonAsync(
            "/api/addresses", BuildPayload(isDefault: false), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var address = await response.Content.ReadFromJsonAsync<AddressDto>(Ct);
        address!.IsDefault.ShouldBeTrue();
    }

    [Fact]
    public async Task Create_SecondAddressWithIsDefault_ClearsPreviousDefault()
    {
        await AuthenticateAsync();
        var first = await CreateAddressAsync(isDefault: true, title: "Ev");

        var response = await Client.PostAsJsonAsync(
            "/api/addresses", BuildPayload(title: "İş", isDefault: true), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var second = await response.Content.ReadFromJsonAsync<AddressDto>(Ct);
        second!.IsDefault.ShouldBeTrue();

        var all = (await Client.GetFromJsonAsync<IReadOnlyList<AddressDto>>("/api/addresses", Ct))!;
        all.Single(a => a.Id == first.Id).IsDefault.ShouldBeFalse();
        all.Single(a => a.Id == second.Id).IsDefault.ShouldBeTrue();
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyOwnAddresses_DefaultFirst()
    {
        await AuthenticateAsync("adres-listesi@test.com");
        await CreateAddressAsync(isDefault: false, title: "İş");
        var defaultAddress = await CreateAddressAsync(isDefault: true, title: "Ev");

        ClearAuthentication();
        await AuthenticateAsync("baska-liste@test.com");
        await CreateAddressAsync(isDefault: true, title: "Diğer kullanıcı");

        ClearAuthentication();
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", await GetAccessTokenAsync("adres-listesi@test.com", "Test1234"));

        var all = await Client.GetFromJsonAsync<IReadOnlyList<AddressDto>>("/api/addresses", Ct);

        all!.Count.ShouldBe(2);
        all[0].Id.ShouldBe(defaultAddress.Id);
        all[0].IsDefault.ShouldBeTrue();
    }

    [Fact]
    public async Task Update_ChangesFields_AndReturnsUpdated()
    {
        await AuthenticateAsync();
        var address = await CreateAddressAsync();

        var response = await Client.PutAsJsonAsync(
            $"/api/addresses/{address.Id}",
            BuildPayload(city: "Ankara", district: "Çankaya", isDefault: address.IsDefault), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<AddressDto>(Ct);
        updated!.City.ShouldBe("Ankara");
        updated.District.ShouldBe("Çankaya");
    }

    [Fact]
    public async Task Update_OfAnotherUsersAddress_Returns403()
    {
        await AuthenticateAsync("adres-guncelle-sahibi@test.com");
        var address = await CreateAddressAsync();

        ClearAuthentication();
        await AuthenticateAsync("adres-guncelle-saldirgan@test.com");

        var response = await Client.PutAsJsonAsync(
            $"/api/addresses/{address.Id}", BuildPayload(), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_WithUnknownId_Returns404()
    {
        await AuthenticateAsync();

        var response = await Client.PutAsJsonAsync("/api/addresses/999999", BuildPayload(), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_Returns204_AndRemovesRow()
    {
        await AuthenticateAsync();
        var address = await CreateAddressAsync();

        var response = await Client.DeleteAsync($"/api/addresses/{address.Id}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var exists = await ExecuteDbAsync(db => db.Addresses.AnyAsync(a => a.Id == address.Id));
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_DefaultAddress_PromotesAnotherAsDefault()
    {
        await AuthenticateAsync();
        var first = await CreateAddressAsync(isDefault: true, title: "Ev");
        var second = await CreateAddressAsync(isDefault: false, title: "İş");

        (await Client.DeleteAsync($"/api/addresses/{first.Id}", Ct)).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        var all = await Client.GetFromJsonAsync<IReadOnlyList<AddressDto>>("/api/addresses", Ct);
        all!.Single(a => a.Id == second.Id).IsDefault.ShouldBeTrue();
    }

    [Fact]
    public async Task Create_WithInvalidPhone_Returns400()
    {
        await AuthenticateAsync();

        var response = await Client.PostAsJsonAsync(
            "/api/addresses", BuildPayload(phone: "abc"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WhenLimitReached_Returns400()
    {
        var userId = await AuthenticateAsync();

        await ExecuteDbAsync(async db =>
        {
            for (var i = 0; i < AddressService.MaxAddressesPerUser; i++)
            {
                db.Addresses.Add(new Address
                {
                    UserId = userId,
                    Title = $"Adres {i}",
                    FullName = "Ali Veli",
                    Phone = "5551112233",
                    City = "İstanbul",
                    District = "Kadıköy",
                    FullAddress = "Moda Caddesi No 1 Daire 5",
                    IsDefault = i == 0
                });
            }
            await db.SaveChangesAsync();
        });

        var response = await Client.PostAsJsonAsync("/api/addresses", BuildPayload(), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
