namespace Commerce.Api.Common.Email;

public sealed class EmailSettings
{
    public const string SectionName = "Email";

    // Testing'de appsettings.json'da "Email" bölümü YOK (ölçüldü) — ValidateOnStart
    // yazılamaz, her alanın anlamlı bir varsayılanı olmalı.
    public string FromAddress { get; init; } = "noreply@commerce.local";
    public string FromName { get; init; } = "E-Commerce";
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 1025;
    public string? Username { get; init; }
    public string? Password { get; init; }
    public bool UseSsl { get; init; }

    /// SMTP donarsa bir Hangfire worker'ı süresiz tutmasın (K7).
    public int TimeoutSeconds { get; init; } = 15;
}
