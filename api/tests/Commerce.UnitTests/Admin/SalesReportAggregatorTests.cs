using Commerce.Api.Features.Admin.Reports;
using Shouldly;

namespace Commerce.UnitTests.Admin;

public class SalesReportAggregatorTests
{
    [Fact]
    public void Fold_Day_ReturnsRowsUnchangedInDateOrder()
    {
        var days = new[]
        {
            new DailySales(new DateOnly(2026, 8, 11), 100m, 2),
            new DailySales(new DateOnly(2026, 8, 10), 50m, 1)
        };

        var result = SalesReportAggregator.Fold(days, ReportGroupBy.Day);

        result.Count.ShouldBe(2);
        result[0].Period.ShouldBe(new DateOnly(2026, 8, 10));
        result[1].Period.ShouldBe(new DateOnly(2026, 8, 11));
    }

    [Fact]
    public void Fold_Month_MergesDaysOfSameMonth()
    {
        var days = new[]
        {
            new DailySales(new DateOnly(2026, 8, 1), 10m, 1),
            new DailySales(new DateOnly(2026, 8, 15), 20m, 2),
            new DailySales(new DateOnly(2026, 8, 31), 30m, 3)
        };

        var result = SalesReportAggregator.Fold(days, ReportGroupBy.Month);

        result.Count.ShouldBe(1);
        result[0].Period.ShouldBe(new DateOnly(2026, 8, 1));
        result[0].Revenue.ShouldBe(60m);
        result[0].OrderCount.ShouldBe(6);
    }

    [Fact]
    public void Fold_Week_UsesIsoMondayAsPeriod()
    {
        // 2026-08-13 Perşembe → o haftanın Pazartesi'si 2026-08-10.
        var days = new[] { new DailySales(new DateOnly(2026, 8, 13), 100m, 1) };

        var result = SalesReportAggregator.Fold(days, ReportGroupBy.Week);

        result.Count.ShouldBe(1);
        result[0].Period.ShouldBe(new DateOnly(2026, 8, 10));
    }

    [Fact]
    public void Fold_Week_AtYearBoundary_KeepsIsoWeekTogether()
    {
        // 31 Aralık 2026 (Perşembe) + 1 Ocak 2027 (Cuma) aynı ISO haftasında.
        var days = new[]
        {
            new DailySales(new DateOnly(2026, 12, 31), 50m, 1),
            new DailySales(new DateOnly(2027, 1, 1), 50m, 1)
        };

        var result = SalesReportAggregator.Fold(days, ReportGroupBy.Week);

        result.Count.ShouldBe(1);
        result[0].Revenue.ShouldBe(100m);
        result[0].OrderCount.ShouldBe(2);
    }

    [Fact]
    public void Fold_Empty_ReturnsEmpty()
    {
        var result = SalesReportAggregator.Fold([], ReportGroupBy.Day);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Fold_PreservesTotalRevenue()
    {
        var days = new[]
        {
            new DailySales(new DateOnly(2026, 8, 1), 10.10m, 1),
            new DailySales(new DateOnly(2026, 8, 2), 20.20m, 2),
            new DailySales(new DateOnly(2026, 8, 3), 30.30m, 3)
        };
        var expectedTotal = days.Sum(d => d.Revenue);

        var month = SalesReportAggregator.Fold(days, ReportGroupBy.Month);
        var week = SalesReportAggregator.Fold(days, ReportGroupBy.Week);
        var day = SalesReportAggregator.Fold(days, ReportGroupBy.Day);

        month.Sum(x => x.Revenue).ShouldBe(expectedTotal);
        week.Sum(x => x.Revenue).ShouldBe(expectedTotal);
        day.Sum(x => x.Revenue).ShouldBe(expectedTotal);
    }
}
