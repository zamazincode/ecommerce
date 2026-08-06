namespace Commerce.Domain.Catalog;

/// many to many bir kitabın birden fazla yazarı olabilir.
public class ProductAuthor
{
    public int ProductId { get; set; }
    public int AuthorId { get; set; }

    public Product Product { get; set; } = null!;
    public Author Author { get; set; } = null!;
}