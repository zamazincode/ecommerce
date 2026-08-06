namespace Commerce.Domain.Catalog;

public class Publisher
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;

    public ICollection<Product> Products { get; set; } = [];
}