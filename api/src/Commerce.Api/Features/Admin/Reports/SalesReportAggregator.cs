using System.Globalization;

namespace Commerce.Api.Features.Admin.Reports;

public enum ReportGroupBy { Day, Week, Month }

/// SQL'den dönen GÜNLÜK satırlar (K6 — Npgsql 10.0.3'te EF.Functions.DateTrunc
/// yok; GroupBy(DateOnly.FromDateTime(...)) ile gün bazında tek sorgu atılır).
public sealed record DailySales(DateOnly Day, decimal Revenue, int OrderCount);

public sealed record SalesReportItemDto(DateOnly Period, decimal Revenue, int OrderCount);

/// Saf statik sınıf: veritabanı yok, saat yok — gün satırlarını hafta/aya
/// BELLEKTE katlar. Aralık en fazla 366 gün olacak şekilde sınırlandığı için
/// (validator) bu katlamanın maliyeti göz ardı edilebilir düzeyde.
public static class SalesReportAggregator
{
    public static IReadOnlyList<SalesReportItemDto> Fold(
        IEnumerable<DailySales> days, ReportGroupBy groupBy) => groupBy switch
    {
        ReportGroupBy.Day => days
            .OrderBy(d => d.Day)
            .Select(d => new SalesReportItemDto(d.Day, d.Revenue, d.OrderCount))
            .ToList(),

        ReportGroupBy.Week => days
            .GroupBy(d => IsoWeekMonday(d.Day))
            .OrderBy(g => g.Key)
            .Select(g => new SalesReportItemDto(g.Key, g.Sum(x => x.Revenue), g.Sum(x => x.OrderCount)))
            .ToList(),

        ReportGroupBy.Month => days
            .GroupBy(d => new DateOnly(d.Day.Year, d.Day.Month, 1))
            .OrderBy(g => g.Key)
            .Select(g => new SalesReportItemDto(g.Key, g.Sum(x => x.Revenue), g.Sum(x => x.OrderCount)))
            .ToList(),

        _ => throw new ArgumentOutOfRangeException(nameof(groupBy))
    };

    /// Dönem etiketi: o ISO haftasının PAZARTESİ'si. Yıl sonu/başı geçişlerinde
    /// ISOWeek takvim yılı sınırını değil ISO hafta sınırını esas alır — 31
    /// Aralık + 1 Ocak aynı ISO haftasındaysa tek satırda kalır.
    private static DateOnly IsoWeekMonday(DateOnly day)
    {
        var date = day.ToDateTime(TimeOnly.MinValue);
        var year = ISOWeek.GetYear(date);
        var week = ISOWeek.GetWeekOfYear(date);
        return DateOnly.FromDateTime(ISOWeek.ToDateTime(year, week, DayOfWeek.Monday));
    }
}
