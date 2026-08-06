using Commerce.Domain.Catalog;
using Shouldly;

namespace Commerce.UnitTests.Catalog;

public class ProductPricingTests
{
    [Fact]
    public void EffectivePrice_WhenDiscountExists_ReturnsDiscountedPrice()
    {
        var product = new Product { Price = 150m, DiscountedPrice = 120m };

        product.EffectivePrice.ShouldBe(120m);
    }

    [Fact]
    public void EffectivePrice_WhenNoDiscount_ReturnsNormalPrice()
    {
        var product = new Product { Price = 150m, DiscountedPrice = null };

        product.EffectivePrice.ShouldBe(150m);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(50, true)]
    public void IsInStock_ReflectsStockLevel(int stock, bool expected)
    {
        new Product { Stock = stock }.IsInStock.ShouldBe(expected);
    }
}
