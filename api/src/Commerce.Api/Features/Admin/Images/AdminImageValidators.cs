using Commerce.Api.Common.Images;
using Commerce.Api.Features.Admin.Images.Dtos;
using FluentValidation;

namespace Commerce.Api.Features.Admin.Images;

public sealed class AddProductImageRequestValidator : AbstractValidator<AddProductImageRequest>
{
    public AddProductImageRequestValidator()
    {
        RuleFor(x => x.PublicId)
            .NotEmpty()
            .Must(CloudinaryPublicId.IsValid)
            .WithMessage("Geçersiz Cloudinary public_id.");

        RuleFor(x => x.DisplayOrder)
            .InclusiveBetween(0, 99)
            .When(x => x.DisplayOrder.HasValue);
    }
}
