namespace Commerce.Api.Features.Admin.Reports;

public sealed record DashboardSummaryDto(
    int TotalProducts, int OutOfStockProducts, int LowStockProducts,
    int TotalOrders, int PendingOrders, decimal TotalRevenue,
    decimal Last30DaysRevenue, int TotalCustomers);

/// ResultCount'un MİNİMUMU alınıyor: "bu terim en az bir kez 0 sonuç döndürdü
/// mü" sorusu, katalog eksiğini gösteren asıl bilgi (PLAN.md Faz 4/7).
public sealed record TopSearchDto(string Term, int SearchCount, int MinResultCount);

public sealed record SalesReportFilterRequest
{
    // DateOnly? — [AsParameters] tuzağı (CLAUDE.md) + query string'ten gelen
    // DateTime hiçbir biçimde Kind=Utc olmuyor (plan 2.4).
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public string? GroupBy { get; init; }
}
