using Commerce.Api.Common.Exceptions;

namespace Commerce.Api.Features.Admin.Reports;

public static class AdminReportEndpoints
{
    public static RouteGroupBuilder MapAdminReportEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/dashboard", GetDashboard)
             .WithSummary("Ürün/sipariş/ciro özeti (60 sn cache)")
             .Produces<DashboardSummaryDto>();

        group.MapGet("/reports/sales", GetSalesReport)
             .WithSummary("Gün/hafta/ay bazında satış raporu (varsayılan: son 30 gün)")
             .Produces<IReadOnlyList<SalesReportItemDto>>()
             .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/reports/top-searches", GetTopSearches)
             .WithSummary("Son 30 günün en çok aranan terimleri")
             .Produces<IReadOnlyList<TopSearchDto>>();

        return group;
    }

    private static async Task<DashboardSummaryDto> GetDashboard(
        AdminReportService service, CancellationToken ct)
        => await service.GetDashboardAsync(ct);

    private static async Task<IReadOnlyList<SalesReportItemDto>> GetSalesReport(
        [AsParameters] SalesReportFilterRequest filter, AdminReportService service, CancellationToken ct)
    {
        var groupBy = ReportGroupBy.Day;
        if (!string.IsNullOrWhiteSpace(filter.GroupBy) &&
            !Enum.TryParse(filter.GroupBy, ignoreCase: true, out groupBy))
            throw new BusinessRuleException(
                $"'{filter.GroupBy}' geçerli bir gruplama değil. Geçerli değerler: Day, Week, Month.");

        return await service.GetSalesReportAsync(filter.From, filter.To, groupBy, ct);
    }

    private static async Task<IReadOnlyList<TopSearchDto>> GetTopSearches(
        AdminReportService service, CancellationToken ct)
        => await service.GetTopSearchTermsAsync(ct: ct);
}
