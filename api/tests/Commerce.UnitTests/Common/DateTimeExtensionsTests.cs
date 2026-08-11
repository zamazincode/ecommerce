using Commerce.Api.Common.Extensions;
using Shouldly;

namespace Commerce.UnitTests.Common;

public class DateTimeExtensionsTests
{
    [Fact]
    public void AsUtc_WhenUtc_ReturnsSame()
    {
        var value = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);

        var result = value.AsUtc();

        result.ShouldBe(value);
        result.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void AsUtc_WhenLocal_ConvertsToUtc()
    {
        var local = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Local);

        var result = local.AsUtc();

        result.Kind.ShouldBe(DateTimeKind.Utc);
        result.ShouldBe(local.ToUniversalTime());
    }

    [Fact]
    public void AsUtc_WhenUnspecified_MarksAsUtc()
    {
        var unspecified = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Unspecified);

        var result = unspecified.AsUtc();

        // Saat KAYMIYOR, yalnızca Kind değişiyor (2.4'ün tuzağı: dönüşüm YAPMA).
        result.Kind.ShouldBe(DateTimeKind.Utc);
        result.Hour.ShouldBe(12);
        result.Day.ShouldBe(10);
    }

    [Fact]
    public void AsUtc_Nullable_PassesNullThrough()
    {
        DateTime? value = null;

        var result = value.AsUtc();

        result.ShouldBeNull();
    }

    [Fact]
    public void StartOfDayUtc_HasUtcKindAndMidnight()
    {
        var day = new DateOnly(2026, 8, 10);

        var result = day.StartOfDayUtc();

        result.Kind.ShouldBe(DateTimeKind.Utc);
        result.ShouldBe(new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void EndOfDayExclusiveUtc_IsNextDayMidnight()
    {
        var day = new DateOnly(2026, 8, 10);

        var result = day.EndOfDayExclusiveUtc();

        // "23:59:59" hilesi DEĞİL — ertesi günün 00:00'ı, dışlayıcı sınır.
        result.ShouldBe(new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc));
    }
}
