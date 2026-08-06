using Commerce.Domain.Pricing;
using Shouldly;

namespace Commerce.UnitTests.Pricing;

public class ShippingCalculatorTests
{
    [Fact]
    public void Calculate_WhenTotalBelowThreshold_ReturnsStandardCost()
    {
        // Arrange
        const decimal subTotal = 150m;

        // Act
        var result = ShippingCalculator.Calculate(subTotal);

        // Assert
        result.ShouldBe(29.90m);
    }

    [Fact]
    public void Calculate_WhenTotalAboveThreshold_ReturnsZero()
    {
        // Arrange
        const decimal subTotal = 250m;

        // Act
        var result = ShippingCalculator.Calculate(subTotal);

        // Assert
        result.ShouldBe(0m);
    }

    [Fact]
    public void Calculate_WhenTotalExactlyAtThreshold_ReturnsZero()
    {
        // Sınır değeri testi: "200₺ ÜSTÜ" mü "200₺ VE ÜSTÜ" mü?
        // Kararı burada sabitliyoruz: 200 dahil, kargo bedava.
        var result = ShippingCalculator.Calculate(200m);

        result.ShouldBe(0m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Calculate_WhenTotalIsZeroOrNegative_ReturnsZero(decimal subTotal)
    {
        var result = ShippingCalculator.Calculate(subTotal);

        result.ShouldBe(0m);
    }
}