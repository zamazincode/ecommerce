using Commerce.Api.Features.Admin.Products.Dtos;
using FluentValidation;

namespace Commerce.Api.Features.Admin.Products;

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Sku).MaximumLength(64);
        RuleFor(x => x.Description).MaximumLength(8000);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.DiscountedPrice)
            .GreaterThan(0)
            .LessThan(x => x.Price)
            .When(x => x.DiscountedPrice.HasValue)
            .WithMessage("İndirimli fiyat, normal fiyattan düşük olmalı.");
        RuleFor(x => x.Stock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CategoryId).GreaterThan(0);
    }
}

public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(8000);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.DiscountedPrice)
            .GreaterThan(0)
            .LessThan(x => x.Price)
            .When(x => x.DiscountedPrice.HasValue)
            .WithMessage("İndirimli fiyat, normal fiyattan düşük olmalı.");
        RuleFor(x => x.CategoryId).GreaterThan(0);
    }
}

public sealed class UpdateStockRequestValidator : AbstractValidator<UpdateStockRequest>
{
    public UpdateStockRequestValidator()
    {
        // Üst sınır parmak kayması koruması — sıfırdan fazla basılan bir "0" DB'nin
        // ck_products_stock_non_negative'ine kadar gitmeden burada yakalanır.
        RuleFor(x => x.Stock).InclusiveBetween(0, 1_000_000);
    }
}

public sealed class BulkPriceUpdateRequestValidator : AbstractValidator<BulkPriceUpdateRequest>
{
    public BulkPriceUpdateRequestValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleFor(x => x.Items).Must(items => items.Count <= 500)
            .WithMessage("Tek istekte en fazla 500 ürün güncellenebilir.");

        // Aynı ürün iki kez gelirse hangi fiyatın kazandığı belirsiz olurdu.
        RuleFor(x => x.Items)
            .Must(items => items.Select(i => i.ProductId).Distinct().Count() == items.Count)
            .WithMessage("Aynı ürün listede birden fazla kez geçemez.")
            .When(x => x.Items.Count > 0);

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0);
            item.RuleFor(i => i.Price).GreaterThan(0);
            item.RuleFor(i => i.DiscountedPrice)
                .GreaterThan(0)
                .LessThan(i => i.Price)
                .When(i => i.DiscountedPrice.HasValue);
        });
    }
}
