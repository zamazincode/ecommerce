namespace Commerce.Domain.Catalog;

/// Kitap dışı ürünlerin markası (JBL, Anatolian, Victorinox...).
/// Publisher'ın kitap dışı karşılığı: kitapta yayınevi, üründe marka.
/// Bir ürünün ikisinden en fazla biri dolu olur.
public class Brand
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;

    public ICollection<Product> Products { get; set; } = [];
}
