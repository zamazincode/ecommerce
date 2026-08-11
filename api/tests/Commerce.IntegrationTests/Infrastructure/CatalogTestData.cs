using Commerce.Api.Persistence;
using Commerce.Domain.Catalog;
using Commerce.Domain.Common;

namespace Commerce.IntegrationTests.Infrastructure;

/// Builder deseni: varsayılan geçerli bir ürün üretir, sadece o testte
/// önemli olan alanı değiştirirsin. Böylece test okunduğunda
/// "bu testte önemli olan tek şey stok = 0" hemen görülür.
public sealed class ProductBuilder
{
    private static int _counter;

    private string _name = "Test Kitabı";
    private string _description = "Test açıklaması.";
    private decimal _price = 100m;
    private decimal? _discountedPrice;
    private int _stock = 10;
    private bool _isActive = true;
    private Category? _category;
    private Author? _author;
    private string? _publisherName;
    private DateTime _createdAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private string? _cloudinaryPublicId;

    public ProductBuilder WithName(string name) { _name = name; return this; }
    public ProductBuilder WithDescription(string description) { _description = description; return this; }
    public ProductBuilder WithPrice(decimal price) { _price = price; return this; }
    public ProductBuilder WithDiscount(decimal? price) { _discountedPrice = price; return this; }
    public ProductBuilder WithStock(int stock) { _stock = stock; return this; }
    public ProductBuilder Inactive() { _isActive = false; return this; }
    public ProductBuilder InCategory(Category c) { _category = c; return this; }
    public ProductBuilder ByAuthor(Author a) { _author = a; return this; }
    public ProductBuilder WithPublisherName(string name) { _publisherName = name; return this; }
    public ProductBuilder CreatedAt(DateTime utc) { _createdAt = utc; return this; }

    /// Varsayılan D&R görselinin yerine Cloudinary'de barınan bir görsel koyar.
    public ProductBuilder WithCloudinaryImage(string publicId = "products/testkapak")
    { _cloudinaryPublicId = publicId; return this; }

    public Product Build()
    {
        var id = Interlocked.Increment(ref _counter);

        var product = new Product
        {
            Name = _name,
            // Slug unique olmak zorunda — testler arası çakışmasın diye sayaç ekliyoruz.
            Slug = $"{SlugGenerator.Generate(_name)}-{id}",
            Description = _description,
            Price = _price,
            DiscountedPrice = _discountedPrice,
            Stock = _stock,
            IsActive = _isActive,
            CreatedAt = _createdAt,
            Category = _category ?? CatalogTestData.DefaultCategory(),
            AuthorNames = _author?.Name,
            PublisherName = _publisherName,
            Images = [_cloudinaryPublicId is null
                ? new ProductImage { SourceUrl = "https://example.test/kapak.jpg", DisplayOrder = 0 }
                : new ProductImage
                {
                    SourceUrl = $"https://res.cloudinary.com/test-cloud/image/upload/{_cloudinaryPublicId}",
                    CloudinaryPublicId = _cloudinaryPublicId,
                    IsMigrated = true,
                    DisplayOrder = 0
                }]
        };

        if (_author is not null)
            product.ProductAuthors = [new ProductAuthor { Author = _author }];

        return product;
    }
}

public static class CatalogTestData
{
    public static Category DefaultCategory(string name = "Roman")
        => new()
        {
            Name = name,
            Slug = SlugGenerator.Generate(name) + "-" + Guid.NewGuid().ToString("N")[..6]
        };

    /// Testte kullanılacak minimum katalog: bir kategori + n ürün.
    public static async Task<Category> SeedCategoryWithProductsAsync(
        AppDbContext db, int productCount, Action<ProductBuilder, int>? customize = null)
    {
        var category = DefaultCategory();
        db.Categories.Add(category);

        for (var i = 0; i < productCount; i++)
        {
            var builder = new ProductBuilder()
                .WithName($"Ürün {i:D3}")
                .InCategory(category)
                .WithPrice(50m + i)
                .CreatedAt(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i));

            customize?.Invoke(builder, i);
            db.Products.Add(builder.Build());
        }

        await db.SaveChangesAsync();
        return category;
    }
}
