namespace Commerce.Domain.Pricing;

/// Sepet üst sınırları. CartCalculator'dan ayrı duruyor: orası SADECE para hesabı.
public static class CartLimits
{
    /// Bir üründen en fazla kaç adet. Yoksa biri 999999 girer (PLAN.md Faz 6).
    public const int MaxQuantityPerLine = 10;

    /// Sepette en fazla kaç FARKLI ürün. Misafir sepeti tamamen anonim
    /// yazılabildiği için sınırsız satır = Redis'te sınırsız büyüyen değer.
    public const int MaxLinesPerCart = 50;
}
