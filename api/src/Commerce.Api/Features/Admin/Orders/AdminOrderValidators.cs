using Commerce.Api.Features.Admin.Orders.Dtos;
using FluentValidation;

namespace Commerce.Api.Features.Admin.Orders;

public sealed class UpdateOrderStatusRequestValidator : AbstractValidator<UpdateOrderStatusRequest>
{
    public UpdateOrderStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}
