namespace Commerce.Api.Common.Extensions;

/// Faz 11'de ölçüldü (plan 2.4): Npgsql `timestamptz` kolonuna YALNIZCA
/// Kind=Utc yazılabiliyor — Unspecified VE Local ikisi de
/// "Cannot write DateTime with Kind=Unspecified/Local ..." ile patlıyor.
/// Dışarıdan (query string, JSON gövde) gelen HER tarih buradan geçmeli.
public static class DateTimeExtensions
{
    public static DateTime AsUtc(this DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        // Saat dilimi belirtilmemişse UTC SAYILIYOR (İstanbul yerel saati değil).
        // Admin arayüzü tarih gönderirken "…Z" eklemeli.
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public static DateTime? AsUtc(this DateTime? value) => value?.AsUtc();

    /// Rapor/filtre aralıklarının ALT sınırı: gün UTC 00:00'da başlar.
    /// DateOnly.ToDateTime(TimeOnly) — İKİ parametreli aşırı yükleme —
    /// Kind=Unspecified üretir (2.4'ün tuzağı); üç parametreli açıkça Utc istiyor.
    public static DateTime StartOfDayUtc(this DateOnly d)
        => d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    /// ÜST sınır: ertesi günün UTC 00:00'ı, DIŞLAYICI (`<`). "23:59:59.999" hilesi
    /// kullanılmıyor — mikrosaniyeli bir kayıt o hile ile sessizce dışarıda kalırdı.
    public static DateTime EndOfDayExclusiveUtc(this DateOnly d)
        => d.AddDays(1).StartOfDayUtc();
}
