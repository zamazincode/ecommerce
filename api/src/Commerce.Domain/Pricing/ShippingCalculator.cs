namespace Commerce.Domain.Pricing;

public static class ShippingCalculator
{
    public const decimal FreeShippingThreshold = 200m;
    public const decimal StandardShippingCost = 29.90m;

    /// <param name="subTotal">İndirimler uygulandıktan SONRAKİ ara toplam.</param>
    public static decimal Calculate(decimal subTotal)
    {
        if (subTotal <= 0m) return 0m;
        return subTotal >= FreeShippingThreshold ? 0m : StandardShippingCost;
    }

}