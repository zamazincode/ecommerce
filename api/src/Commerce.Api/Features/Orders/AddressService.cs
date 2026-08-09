using Commerce.Api.Common.Exceptions;
using Commerce.Api.Features.Orders.Dtos;
using Commerce.Api.Persistence;
using Commerce.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Api.Features.Orders;

public sealed class AddressService(AppDbContext db)
{
    /// Kullanıcının yazabildiği bir tablo — üst sınırsız bırakma.
    public const int MaxAddressesPerUser = 20;

    public async Task<IReadOnlyList<AddressDto>> GetAllAsync(Guid userId, CancellationToken ct = default)
        => await db.Addresses
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault).ThenBy(a => a.Id)
            .Select(a => new AddressDto(
                a.Id, a.Title, a.FullName, a.Phone,
                a.City, a.District, a.FullAddress, a.IsDefault))
            .ToListAsync(ct);

    public async Task<AddressDto> CreateAsync(
        Guid userId, SaveAddressRequest request, CancellationToken ct = default)
    {
        var count = await db.Addresses.CountAsync(a => a.UserId == userId, ct);
        if (count >= MaxAddressesPerUser)
            throw new BusinessRuleException(
                $"En fazla {MaxAddressesPerUser} adres kaydedebilirsiniz.");

        var address = new Address
        {
            UserId = userId,
            Title = request.Title,
            FullName = request.FullName,
            Phone = request.Phone,
            City = request.City,
            District = request.District,
            FullAddress = request.FullAddress,
            IsDefault = request.IsDefault
        };

        // İlk adres otomatik varsayılan olsun.
        var isFirst = count == 0;
        if (isFirst) address.IsDefault = true;

        if (address.IsDefault) await ClearOtherDefaultsAsync(userId, ct);

        db.Addresses.Add(address);
        await db.SaveChangesAsync(ct);

        return ToDto(address);
    }

    public async Task<AddressDto> UpdateAsync(
        Guid userId, int addressId, SaveAddressRequest request, CancellationToken ct = default)
    {
        var address = await LoadOwnedAsync(userId, addressId, ct);

        address.Title = request.Title;
        address.FullName = request.FullName;
        address.Phone = request.Phone;
        address.City = request.City;
        address.District = request.District;
        address.FullAddress = request.FullAddress;

        if (request.IsDefault && !address.IsDefault)
        {
            await ClearOtherDefaultsAsync(userId, ct);
            address.IsDefault = true;
        }

        await db.SaveChangesAsync(ct);
        return ToDto(address);
    }

    public async Task DeleteAsync(Guid userId, int addressId, CancellationToken ct = default)
    {
        var address = await LoadOwnedAsync(userId, addressId, ct);

        db.Addresses.Remove(address);
        await db.SaveChangesAsync(ct);

        // Varsayılan adres silindiyse kalanlardan birini varsayılan yap.
        if (!address.IsDefault) return;

        var replacement = await db.Addresses
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.Id)
            .FirstOrDefaultAsync(ct);

        if (replacement is null) return;

        replacement.IsDefault = true;
        await db.SaveChangesAsync(ct);
    }

    /// SAHİPLİK KONTROLÜ TEK YERDE.
    /// Her metotta ayrı ayrı yazsaydık, bir gün birinde unuturduk — IDOR açığı
    /// tam olarak böyle doğar.
    internal async Task<Address> LoadOwnedAsync(Guid userId, int addressId, CancellationToken ct)
    {
        var address = await db.Addresses.FirstOrDefaultAsync(a => a.Id == addressId, ct)
            ?? throw NotFoundException.For("Adres", addressId);

        if (address.UserId != userId)
            throw new ForbiddenException("Bu adres size ait değil.");

        return address;
    }

    private async Task ClearOtherDefaultsAsync(Guid userId, CancellationToken ct)
        => await db.Addresses
            .Where(a => a.UserId == userId && a.IsDefault)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false), ct);

    private static AddressDto ToDto(Address a)
        => new(a.Id, a.Title, a.FullName, a.Phone, a.City, a.District, a.FullAddress, a.IsDefault);
}
