using Commerce.Api.Common.Results;
using Commerce.Api.Features.Catalog.Dtos;

namespace Commerce.Api.Features.Search;

/// Minimal API [AsParameters] ile query string'den bağlanır.
/// ?q=suc%20ve%20ceza&amp;page=1&amp;pageSize=20&amp;categoryId=5&amp;minPrice=50
public sealed record SearchRequest
{
    // DİKKAT: [AsParameters] non-nullable property'leri ZORUNLU sayar
    // (CLAUDE.md tuzak tablosu). Opsiyonel query parametreleri her zaman
    // nullable olmalı; varsayılan değerler ToPageRequest() içinde verilir.
    public string? Q { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public int? CategoryId { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public bool? InStock { get; init; }

    public PageRequest ToPageRequest() => new()
    {
        Page = Page ?? 1,
        PageSize = PageSize ?? PageRequest.DefaultPageSize
    };
}

public sealed record SearchResultDto(
    PagedResult<ProductListDto> Results,
    string Term,
    /// Sonuç 0 döndüyse ve yakın bir yazar/yayınevi varsa dolar, aksi hâlde null.
    string? DidYouMean);

public sealed record SuggestionDto(string Slug, string Name, string? ImageUrl);

/// Faz E1'de ayrı bir arama motoru (Elasticsearch/Meilisearch) gelirse bu
/// arayüzü implement eden yeni bir sınıf yazılır; DI kaydı tek satır değişir,
/// endpoint'ler ve testler arayüze bağlı olduğu için etkilenmez.
public interface ISearchService
{
    Task<SearchResultDto> SearchAsync(SearchRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<SuggestionDto>> SuggestAsync(string? term, CancellationToken ct = default);
}
