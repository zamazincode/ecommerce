using Commerce.Domain.Common;

namespace Commerce.Api.Persistence.Seeding.Import;

/// Ayrıştırılmış, doğrulanmış satır. Buradan sonrası saf entity kurma işi.
public sealed record ProductImportRow
{
    public required string Sku { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }

    public required decimal Price { get; init; }
    public decimal? DiscountedPrice { get; init; }

    public required IReadOnlyList<string> CategoryPath { get; init; }
    public string? PublisherName { get; init; }
    public string? BrandName { get; init; }
    public required IReadOnlyList<string> AuthorNames { get; init; }
    public required IReadOnlyList<string> ImageUrls { get; init; }

    /// Kitap dışı ürünlere (puzzle, kulaklık, defter) BookDetail açılmaz.
    public bool IsBook { get; init; }
    public string? Isbn { get; init; }
    public int? PageCount { get; init; }
    public int? PublishedYear { get; init; }
    public string? Language { get; init; }
    public BookBinding Binding { get; init; }
}
