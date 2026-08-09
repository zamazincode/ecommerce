namespace Commerce.Api.Features.Orders.Dtos;

public sealed record AddressDto(
    int Id, string Title, string FullName, string Phone,
    string City, string District, string FullAddress, bool IsDefault);

public sealed record SaveAddressRequest(
    string Title, string FullName, string Phone,
    string City, string District, string FullAddress, bool IsDefault);
