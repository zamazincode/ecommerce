using Commerce.Api.Common.Caching;
using Commerce.Api.Common.Exceptions;
using Commerce.Api.Common.Extensions;
using Commerce.Api.Persistence;
using Commerce.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Commerce.Api.Features.Admin.Reports;

public sealed class AdminReportService(AppDbContext db, HybridCache cache, TimeProvider clock)
{
    /// Validator: SQL'den dönen satır sayısını sabitler, bellekte hafta/ay
    /// katlamasının maliyetini göz ardı edilebilir düzeyde tutar (K6).
    public const int MaxReportDays = 366;

    /// 0 olanlar "tükendi" sayacına giriyor — "azaldı" ile aynı kutuya girmemeli.
    private const int LowStockThreshold = 5;

    public async Task<DashboardSummaryDto> GetDashboardAsync(CancellationToken ct = default)
        => await cache.GetOrCreateAsync(
            CacheKeys.AdminDashboard,
            this,
            static async (service, token) => await service.LoadDashboardAsync(token),
            // 15 dk DEĞİL 60 sn (§0'ın bilinçli kararı): dashboard sunumunda
            // "sipariş verdim, sayaç değişmedi" izlenimi vermesin.
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(60) },
            tags: [CacheTags.Products, CacheTags.Orders],
            cancellationToken: ct);

    private async Task<DashboardSummaryDto> LoadDashboardAsync(CancellationToken ct)
    {
        var totalProducts = await db.Products.CountAsync(ct);
        var outOfStock = await db.Products.CountAsync(p => p.Stock == 0, ct);
        var lowStock = await db.Products.CountAsync(p => p.Stock >= 1 && p.Stock <= LowStockThreshold, ct);

        var totalOrders = await db.Orders.CountAsync(ct);
        var pendingOrders = await db.Orders.CountAsync(o => o.Status == OrderStatus.Pending, ct);

        // Ciro: Paid|Delivered. Cancelled/Refunded HARİÇ — para gerçekten
        // firmada kalan siparişler sayılır.
        var totalRevenue = await db.Orders
            .Where(o => o.Status == OrderStatus.Paid || o.Status == OrderStatus.Delivered)
            .SumAsync(o => (decimal?)o.Total, ct) ?? 0m;

        var since = clock.GetUtcNow().UtcDateTime.AddDays(-30);
        var last30DaysRevenue = await db.Orders
            .Where(o => (o.Status == OrderStatus.Paid || o.Status == OrderStatus.Delivered) && o.CreatedAt >= since)
            .SumAsync(o => (decimal?)o.Total, ct) ?? 0m;

        var totalCustomers = await db.Users.CountAsync(ct);

        return new DashboardSummaryDto(
            totalProducts, outOfStock, lowStock, totalOrders, pendingOrders,
            totalRevenue, last30DaysRevenue, totalCustomers);
    }

    public async Task<IReadOnlyList<SalesReportItemDto>> GetSalesReportAsync(
        DateOnly? fromParam, DateOnly? toParam, ReportGroupBy groupBy, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var from = fromParam ?? today.AddDays(-29);
        var to = toParam ?? today;

        if (to < from)
            throw new BusinessRuleException("Bitiş tarihi başlangıç tarihinden önce olamaz.");

        if (to.DayNumber - from.DayNumber > MaxReportDays)
            throw new BusinessRuleException($"Rapor aralığı en fazla {MaxReportDays} gün olabilir.");

        return await cache.GetOrCreateAsync(
            CacheKeys.AdminSales(from, to, groupBy),
            (Service: this, From: from, To: to, GroupBy: groupBy),
            static async (state, token)
                => await state.Service.LoadSalesReportAsync(state.From, state.To, state.GroupBy, token),
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(15) },
            tags: [CacheTags.Orders],
            cancellationToken: ct);
    }

    private async Task<IReadOnlyList<SalesReportItemDto>> LoadSalesReportAsync(
        DateOnly from, DateOnly to, ReportGroupBy groupBy, CancellationToken ct)
    {
        var fromUtc = from.StartOfDayUtc();
        var toUtc = to.EndOfDayExclusiveUtc();

        // K6: EF.Functions.DateTrunc Npgsql 10.0.3'te YOK (2.3). Tek sorgu GÜN
        // bazında; hafta/ay SalesReportAggregator.Fold ile bellekte katlanıyor.
        //
        // DİKKAT: OrderBy GROUP KEY üzerinden, Select'TEN ÖNCE yapılıyor.
        // Projeksiyondan (DailySales'e Select) SONRA OrderBy yazılırsa EF
        // "could not be translated" hatası veriyor (CLAUDE.md kuralı — burada
        // bir kez ölçülerek doğrulandı).
        var rows = await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Paid || o.Status == OrderStatus.Delivered)
            .Where(o => o.CreatedAt >= fromUtc && o.CreatedAt < toUtc)
            .GroupBy(o => DateOnly.FromDateTime(o.CreatedAt))
            .OrderBy(g => g.Key)
            .Select(g => new DailySales(g.Key, g.Sum(o => o.Total), g.Count()))
            .ToListAsync(ct);

        return SalesReportAggregator.Fold(rows, groupBy);
    }

    public async Task<IReadOnlyList<TopSearchDto>> GetTopSearchTermsAsync(
        int days = 30, int take = 20, CancellationToken ct = default)
    {
        var since = clock.GetUtcNow().UtcDateTime.AddDays(-days);

        // Aynı kural: OrderBy grup üzerinden, Select'TEN ÖNCE (bkz. satır 98).
        return await db.SearchLogs.AsNoTracking()
            .Where(l => l.CreatedAt >= since)
            .GroupBy(l => l.Term)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => new TopSearchDto(g.Key, g.Count(), g.Min(x => x.ResultCount)))
            .Take(take)
            .ToListAsync(ct);
    }
}
