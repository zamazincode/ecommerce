using Commerce.Api.Common.Results;

namespace Commerce.Api.Features.Catalog;

/// Minimal API [AsParameters] ile query string'den bağlanır.
/// ?page=2&amp;pageSize=24&amp;categoryId=5&amp;minPrice=50&amp;sortBy=price&amp;sortDir=asc
public sealed record ProductFilterRequest
{
    // DİKKAT: [AsParameters] non-nullable property'leri ZORUNLU sayar —
    // "Required parameter "int Page" was not provided from query string" hatası
    // verir. C# tarafındaki "= 1" varsayılanı bağlamayı etkilemiyor.
    // Opsiyonel query parametreleri her zaman nullable olmalı.
    public int? Page { get; init; }
    public int? PageSize { get; init; }

    public int? CategoryId { get; init; }
    public int? AuthorId { get; init; }
    public int? PublisherId { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public bool? InStock { get; init; }

    public string? SortBy { get; init; }
    public string? SortDir { get; init; }

    /// PageRequest üst sınırı (100) ve alt sınırı kendi init'inde uyguluyor.
    public PageRequest ToPageRequest() => new()
    {
        Page = Page ?? 1,
        PageSize = PageSize ?? PageRequest.DefaultPageSize
    };
}
