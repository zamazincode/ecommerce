using Commerce.Api.Common.Caching;
using Commerce.Api.Common.Exceptions;
using Commerce.Api.Common.Extensions;
using Commerce.Api.Features.Catalog;
using Commerce.Api.Persistence;
using Commerce.Domain.Common;
using Commerce.Domain.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using NpgsqlTypes;

namespace Commerce.Api.Features.Search;

public sealed class PostgresSearchService(
    AppDbContext db,
    CategoryService categories,
    HybridCache cache,
    ILogger<PostgresSearchService> logger) : ISearchService
{
    private const string TextConfig = "turkish";

    /// "Bunu mu demek istediniz" eşiği. Ölçüm (Faz 4 planı, 2.9): bu eşikte
    /// tüm doğru öneriler geçiyor, en yüksek yanlış öneri (0.545) eleniyor.
    private const double DidYouMeanThreshold = 0.6;

    public async Task<SearchResultDto> SearchAsync(
        SearchRequest request, CancellationToken ct = default)
    {
        var term = SearchTermNormalizer.Normalize(request.Q);

        // Normalizasyon atlanırsa arama HATA VERMEZ, sessizce eksik sonuç
        // döner (bkz. plan Ölçüm 2.4). Bu yüzden Normalize her girişte
        // zorunlu ve kısa terim burada, normalize edilmiş hâliyle kontrol edilir.
        if (term.Length < SearchTermNormalizer.MinLength)
            throw new BusinessRuleException(
                $"Arama terimi en az {SearchTermNormalizer.MinLength} karakter olmalı.");

        IReadOnlyCollection<int>? categoryIds = null;
        if (request.CategoryId is { } categoryId)
            categoryIds = await categories.GetSelfAndDescendantIdsAsync(categoryId, ct);

        var minPrice = request.MinPrice;
        var maxPrice = request.MaxPrice;

        var query = db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            // Kullanıcı girdisini ASLA ToTsQuery'ye verme — & | ! ( ) : * gibi
            // karakterler sözdizimi hatası fırlatır (500). PlainToTsQuery
            // girdiyi temizler, kelimeleri AND ile birleştirir.
            .Where(p => EF.Property<NpgsqlTsVector>(p, "SearchVector")
                .Matches(EF.Functions.PlainToTsQuery(TextConfig, term)))
            .WhereIf(categoryIds is not null, p => categoryIds!.Contains(p.CategoryId))
            .WhereIf(minPrice.HasValue, p => (p.DiscountedPrice ?? p.Price) >= minPrice!.Value)
            .WhereIf(maxPrice.HasValue, p => (p.DiscountedPrice ?? p.Price) <= maxPrice!.Value)
            .WhereIf(request.InStock == true, p => p.Stock > 0)
            // Alaka skoruna göre sırala: başlıkta geçen (ağırlık A) açıklamada
            // geçenden (ağırlık D) üstte çıkar (bkz. plan Ölçüm 2.8).
            .OrderByDescending(p => EF.Property<NpgsqlTsVector>(p, "SearchVector")
                .Rank(EF.Functions.PlainToTsQuery(TextConfig, term)))
            .ThenBy(p => p.Id);    // deterministik sayfalama

        // Kural: filtrele → sırala → sayfala → EN SONDA Select.
        var paged = await query
            .Select(ProductService.ListProjection)
            .ToPagedResultAsync(request.ToPageRequest(), ct);

        string? didYouMean = null;
        if (paged.TotalCount == 0)
            didYouMean = await FindDidYouMeanAsync(term, ct);

        await LogSearchAsync(term, paged.TotalCount, ct);

        return new SearchResultDto(paged, term, didYouMean);
    }

    public async Task<IReadOnlyList<SuggestionDto>> SuggestAsync(
        string? term, CancellationToken ct = default)
    {
        var normalized = SearchTermNormalizer.Normalize(term);

        // Autocomplete her tuş vuruşunda çağrılıyor — 400 yerine boş liste.
        if (normalized.Length < SearchTermNormalizer.MinLength)
            return [];

        return await cache.GetOrCreateAsync(
            CacheKeys.Suggest(normalized),
            (Service: this, Term: normalized),
            static async (state, token) => await state.Service.LoadSuggestionsAsync(state.Term, token),
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(5) },
            tags: [CacheTags.Products],
            cancellationToken: ct);
    }

    private async Task<IReadOnlyList<SuggestionDto>> LoadSuggestionsAsync(
        string normalized, CancellationToken ct)
        // ILIKE '%...%' normalde index kullanamaz; ix_products_searchname_trgm
        // (gin_trgm_ops) sayesinde burada kullanabiliyor. SearchName ad+yazar
        // birleşimi olduğu için "dosto" yazan kullanıcı Dostoyevski'nin
        // kitaplarını görür — kitapçı arama kutusundan beklenen davranış budur.
        => await db.Products
            .AsNoTracking()
            .Where(p => p.IsActive &&
                        EF.Functions.ILike(EF.Property<string>(p, "SearchName"), $"%{normalized}%"))
            .OrderByDescending(p => EF.Functions.TrigramsWordSimilarity(
                normalized, EF.Property<string>(p, "SearchName")))
            .ThenBy(p => p.Name.Length)    // kısa başlık önce — ws'te beraberlik çok
            .ThenBy(p => p.Id)             // deterministik
            .Take(8)
            .Select(p => new SuggestionDto(
                p.Slug,
                p.Name,
                p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.SourceUrl).FirstOrDefault()))
            .ToListAsync(ct);

    /// Sonuç dönmediğinde en yakın yazar/yayınevi adını öner.
    ///
    /// DİKKAT: kaynak ÜRÜN ADI DEĞİL, yazar/yayınevi. Ürün adları 70-100
    /// karakter olduğu için similarity() tüm dizeyi seyreltiyor ve eşik hiç
    /// dolmuyor (plan Ölçüm 2.9). word_similarity ise dizenin EN İYİ alt
    /// dizisine bakıyor, o yüzden uzunluktan bağımsız çalışıyor.
    private async Task<string?> FindDidYouMeanAsync(string term, CancellationToken ct)
    {
        // DİKKAT: Where/OrderBy EN SONDA gelen Select'ten ÖNCE yazılmalı.
        // Önce NameScore'a projekte edip sonra .Score'a göre filtrelemek
        // "could not be translated" hatası verir (CLAUDE.md kural: filtrele
        // → sırala → sayfala → EN SONDA Select).
        var author = await db.Authors
            .AsNoTracking()
            .Where(a => EF.Functions.TrigramsWordSimilarity(
                term, EF.Functions.Unaccent(a.Name.ToLower())) >= DidYouMeanThreshold)
            .OrderByDescending(a => EF.Functions.TrigramsWordSimilarity(
                term, EF.Functions.Unaccent(a.Name.ToLower())))
            .Select(a => new NameScore(a.Name,
                EF.Functions.TrigramsWordSimilarity(term, EF.Functions.Unaccent(a.Name.ToLower()))))
            .FirstOrDefaultAsync(ct);

        var publisher = await db.Publishers
            .AsNoTracking()
            .Where(p => EF.Functions.TrigramsWordSimilarity(
                term, EF.Functions.Unaccent(p.Name.ToLower())) >= DidYouMeanThreshold)
            .OrderByDescending(p => EF.Functions.TrigramsWordSimilarity(
                term, EF.Functions.Unaccent(p.Name.ToLower())))
            .Select(p => new NameScore(p.Name,
                EF.Functions.TrigramsWordSimilarity(term, EF.Functions.Unaccent(p.Name.ToLower()))))
            .FirstOrDefaultAsync(ct);

        if (author is null) return publisher?.Name;
        if (publisher is null) return author.Name;
        return author.Score >= publisher.Score ? author.Name : publisher.Name;
    }

    /// Aranan terimleri kaydet. İki faydası:
    /// 1. Admin panelde "en çok aranan" raporu (Faz 11)
    /// 2. Sonuç dönmeyen aramalar → katalog eksiğini gösterir
    private async Task LogSearchAsync(string term, int resultCount, CancellationToken ct)
    {
        try
        {
            db.SearchLogs.Add(new SearchLog
            {
                Term = term,
                ResultCount = resultCount,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Log yazımı BAŞARISIZ OLSA BİLE arama sonucu dönmeli — ikincil
            // bir özellik yüzünden ana işlevi bozmuyoruz. ct iptal edilmişse
            // fırlayacak OperationCanceledException de bilerek burada yutulur.
            logger.LogWarning(ex, "Arama logu yazılamadı. Terim: {Term}", term);
        }
    }

    private sealed record NameScore(string Name, double Score);
}
