using Commerce.Api.Features.Orders.Dtos;
using FluentValidation;

namespace Commerce.Api.Features.Orders;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.AddressId).GreaterThan(0).WithMessage("Teslimat adresi seçilmeli.");
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.ExpectedTotal)
            .GreaterThanOrEqualTo(0m).When(x => x.ExpectedTotal.HasValue);
    }
}

public sealed class SaveAddressRequestValidator : AbstractValidator<SaveAddressRequest>
{
    public SaveAddressRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(50);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30)
            .Matches(@"^[0-9\s\+\-\(\)]+$").WithMessage("Telefon numarası geçersiz.");
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.District).NotEmpty().MaximumLength(100);
        RuleFor(x => x.FullAddress).NotEmpty().MinimumLength(10).MaximumLength(1000);
    }
}
