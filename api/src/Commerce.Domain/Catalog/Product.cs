using Commerce.Domain.Common;

namespace Commerce.Domain.Catalog;

public class Product : IAuditable, ISoftDeletable
{
    public int Id { get; set; }

    /// Dış kaynağın (D&R kataloğu, tedarikçi listesi) ürün kodu.
    /// İçe aktarımda eşleştirme anahtarı: aynı SKU iki kez yüklenmez.
    /// Elle eklenen ürünlerde null olabilir.
    public string? Sku { get; set; }

    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? Description { get; set; }

    public decimal Price { get; set; }
    public decimal? DiscountedPrice { get; set; }
    public int Stock { get; set; }

    public int CategoryId { get; set; }
    public int? PublisherId { get; set; }
    public int? BrandId { get; set; }

    // Aramada kullanılmak üzere denormalize edilmiş alanlar.
    public string? AuthorNames { get; set; }
    public string? PublisherName { get; set; }
    public string? BrandName { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Category Category { get; set; } = null!;
    public Publisher? Publisher { get; set; }
    public Brand? Brand { get; set; }
    public BookDetail? BookDetail { get; set; }
    public ICollection<ProductAuthor> ProductAuthors { get; set; } = [];
    public ICollection<ProductImage> Images { get; set; } = [];

    /// Kullanıcının gerçekten ödeyeceği fiyat.
    /// Fiyat filtreleme ve sepet hesabı HER ZAMAN bunu kullanır.
    public decimal EffectivePrice => DiscountedPrice ?? Price;

    public bool IsInStock => Stock > 0;
}