using Commerce.Domain.Common;

namespace Commerce.Domain.Catalog;

/// Kitaba özel alanlar. Product ile 1-1.
public class BookDetail
{
    public int ProductId { get; set; }
    public string? Isbn { get; set; }
    public int? PageCount { get; set; }
    public string? Language { get; set; }
    public int? PublishedYear { get; set; }
    public BookBinding Binding { get; set; }

    public Product Product { get; set; } = null!;
}