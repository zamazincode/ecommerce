using Commerce.Api.Common.Results;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Api.Common.Extensions;

public static class QueryableExtensions
{
    /// İki sorgu atar: COUNT ve sayfa. Bu normaldir ve doğrudur —
    /// toplam sayı olmadan "kaç sayfa var" bilgisini veremezsin.
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, PageRequest page, CancellationToken ct = default)
    {
        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(ct);

        return new PagedResult<T>(items, page.Page, page.PageSize, totalCount);
    }

    public static IQueryable<T> WhereIf<T>(
        this IQueryable<T> query, bool condition, System.Linq.Expressions.Expression<Func<T, bool>> predicate)
        => condition ? query.Where(predicate) : query;
}