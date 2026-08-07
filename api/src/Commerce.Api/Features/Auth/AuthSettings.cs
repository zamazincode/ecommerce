namespace Commerce.Api.Features.Auth;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    /// En az 32 karakter. User Secrets'ta durur, appsettings'e ASLA yazılmaz.
    public string Key { get; init; } = null!;
    public string Issuer { get; init; } = "commerce-api";
    public string Audience { get; init; } = "commerce-clients";
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 30;
}

/// Mail içindeki bağlantıların işaret ettiği ön yüz adresi (K12).
public sealed class WebAppSettings
{
    public const string SectionName = "Web";
    public string BaseUrl { get; init; } = "http://localhost:3000";
}

/// Claim adlarını tek yerden yönet. String'i her yerde elle yazarsan
/// er ya da geç birini yanlış yazarsın.
public static class JwtClaims
{
    public const string Sub = "sub";
    public const string Email = "email";
    public const string Role = "role";
    public const string Jti = "jti";
}

public static class AuthPolicies
{
    public const string AdminOnly = "AdminOnly";
    public const string CanManageProducts = "CanManageProducts";
}
