using Commerce.Api.Features.Catalog;
using Commerce.Domain.Catalog;
using Shouldly;

namespace Commerce.UnitTests.Catalog;

public class ProductSortingTests
{
    private static IQueryable<Product> SampleProducts() => new List<Product>
    {
        new() { Id = 1, Name = "Bir", Price = 100m, DiscountedPrice = 40m, CreatedAt = new DateTime(2026, 1, 1) },
        new() { Id = 2, Name = "Ali", Price = 50m,  DiscountedPrice = null, CreatedAt = new DateTime(2026, 3, 1) },
        new() { Id = 3, Name = "Cem", Price = 70m,  DiscountedPrice = null, CreatedAt = new DateTime(2026, 2, 1) }
    }.AsQueryable();

    [Fact]
    public void ApplySort_ByPriceAsc_UsesDiscountedPriceWhenPresent()
    {
        // Arrange — Id=1 listede en pahalı görünüyor (100₺) ama indirimli fiyatı 40₺.
        var products = SampleProducts();

        // Act
        var result = ProductSorting.ApplySort(products, "price", "asc").ToList();

        // Assert — 40, 50, 70
        result.Select(p => p.Id).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public void ApplySort_ByPriceDesc_ReversesOrder()
    {
        var result = ProductSorting.ApplySort(SampleProducts(), "price", "desc").ToList();

        result.Select(p => p.Id).ShouldBe([3, 2, 1]);
    }

    [Fact]
    public void ApplySort_ByName_SortsAlphabetically()
    {
        var result = ProductSorting.ApplySort(SampleProducts(), "name", "asc").ToList();

        result.Select(p => p.Name).ShouldBe(["Ali", "Bir", "Cem"]);
    }

    [Fact]
    public void ApplySort_ByNewest_ReturnsMostRecentFirst()
    {
        var result = ProductSorting.ApplySort(SampleProducts(), "newest", null).ToList();

        result.Select(p => p.Id).ShouldBe([2, 3, 1]);
    }

    [Theory]
    [InlineData("DROP TABLE Products")]
    [InlineData("id; DELETE FROM Products")]
    [InlineData("bilinmeyen-alan")]
    [InlineData("")]
    [InlineData(null)]
    public void ApplySort_WithUnknownField_FallsBackToDefaultWithoutThrowing(string? sortBy)
    {
        // Beyaz liste dışındaki her şey sessizce varsayılana düşmeli.
        var result = Should.NotThrow(() =>
            ProductSorting.ApplySort(SampleProducts(), sortBy, "asc").ToList());

        result.Count.ShouldBe(3);
        result[0].Id.ShouldBe(2);   // varsayılan: en yeni (2026-03-01)
    }

    [Fact]
    public void ApplySort_AlwaysAppliesSecondaryIdOrdering()
    {
        // Aynı CreatedAt'e sahip ürünler. İkincil sıralama olmasaydı
        // sıra belirsiz olur, sayfalama kayardı.
        var sameDate = new DateTime(2026, 5, 5);
        var products = new List<Product>
        {
            new() { Id = 30, Name = "C", Price = 10m, CreatedAt = sameDate },
            new() { Id = 10, Name = "A", Price = 10m, CreatedAt = sameDate },
            new() { Id = 20, Name = "B", Price = 10m, CreatedAt = sameDate }
        }.AsQueryable();

        var result = ProductSorting.ApplySort(products, "price", "asc").ToList();

        result.Select(p => p.Id).ShouldBe([10, 20, 30]);
    }
}
