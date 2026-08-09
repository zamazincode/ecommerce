using Commerce.Api.Features.Cart.Dtos;
using Commerce.Domain.Pricing;
using FluentValidation;

namespace Commerce.Api.Features.Cart;

public sealed class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, CartLimits.MaxQuantityPerLine)
            .WithMessage($"Adet 1 ile {CartLimits.MaxQuantityPerLine} arasında olmalı.");
    }
}

public sealed class UpdateCartItemRequestValidator : AbstractValidator<UpdateCartItemRequest>
{
    public UpdateCartItemRequestValidator()
        => RuleFor(x => x.Quantity)
            .InclusiveBetween(1, CartLimits.MaxQuantityPerLine)
            .WithMessage($"Adet 1 ile {CartLimits.MaxQuantityPerLine} arasında olmalı.");
}

public sealed class ApplyCouponRequestValidator : AbstractValidator<ApplyCouponRequest>
{
    // Code null gelirse (örn. {} gövdesi) Trim() NullReferenceException atardı → 500.
    // NotEmpty() bu hattı kapatıyor (Faz 5'in T8 tuzağının aynısı).
    public ApplyCouponRequestValidator()
        => RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
}
