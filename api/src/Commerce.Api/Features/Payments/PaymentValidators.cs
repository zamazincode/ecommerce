using Commerce.Api.Features.Payments.Dtos;
using FluentValidation;

namespace Commerce.Api.Features.Payments;

public sealed class InitializePaymentRequestValidator : AbstractValidator<InitializePaymentRequest>
{
    public InitializePaymentRequestValidator()
        // Orders.OrderNumber kolonu 30 karakter — sınır oradan geliyor.
        => RuleFor(x => x.OrderNumber).NotEmpty().MaximumLength(30);
}
