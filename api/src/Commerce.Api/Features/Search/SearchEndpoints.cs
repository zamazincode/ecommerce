namespace Commerce.Api.Features.Search;

public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/search").WithTags("Search");

        group.MapGet("/", Search)
             .WithSummary("Ürün araması")
             .WithDescription("Full-text arama (PostgreSQL FTS). Sonuç yoksa DidYouMean alanı dolabilir.")
             .Produces<SearchResultDto>()
             .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/suggest", Suggest)
             .WithSummary("Autocomplete önerileri (en fazla 8)")
             .Produces<IReadOnlyList<SuggestionDto>>();

        return app;
    }

    private static async Task<SearchResultDto> Search(
        [AsParameters] SearchRequest request, ISearchService service, CancellationToken ct)
        => await service.SearchAsync(request, ct);

    private static async Task<IReadOnlyList<SuggestionDto>> Suggest(
        string? q, ISearchService service, CancellationToken ct)
        => await service.SuggestAsync(q, ct);
}
