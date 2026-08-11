namespace Commerce.Api.Common.Images;

/// Testing'de okunan appsettings.json'da "Cloudinary" bölümü YOK (EmailSettings'le
/// aynı durum) — ValidateOnStart YAZILAMAZ, her alanın boş varsayılanı olmalı.
public sealed class CloudinarySettings
{
    public const string SectionName = "Cloudinary";

    public string CloudName { get; init; } = "";
    public string ApiKey { get; init; } = "";
    public string ApiSecret { get; init; } = "";
}
