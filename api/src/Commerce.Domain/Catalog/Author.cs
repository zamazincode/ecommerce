namespace Commerce.Domain.Catalog;

public class Author
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? Bio { get; set; }

    public ICollection<ProductAuthor> ProductAuthors { get; set; } = [];
}