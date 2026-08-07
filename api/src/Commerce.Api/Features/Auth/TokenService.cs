using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Commerce.Api.Persistence.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Commerce.Api.Features.Auth;

public sealed class TokenService(IOptions<JwtSettings> options, TimeProvider clock)
{
    private readonly JwtSettings _settings = options.Value;

    public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(
        ApplicationUser user, IEnumerable<string> roles, TimeSpan? lifetime = null)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expiresAt = now.Add(lifetime ?? TimeSpan.FromMinutes(_settings.AccessTokenMinutes));

        // "Süresi geçmiş token" testleri negatif lifetime veriyor (expiresAt < now).
        // JwtSecurityToken constructor'ı notBefore >= expires olduğunda IDX12401
        // fırlatır; notBefore'u her zaman expiresAt'ten kesinlikle önce tutuyoruz.
        // Normal (pozitif) senaryoda davranış değişmiyor: notBefore yine "now".
        var notBefore = expiresAt <= now ? expiresAt.AddSeconds(-1) : now;

        var claims = new List<Claim>
        {
            new(JwtClaims.Sub, user.Id.ToString()),
            new(JwtClaims.Email, user.Email ?? string.Empty),
            // jti: token'ın benzersiz kimliği. İleride kara listeye almak istersen gerekir.
            new(JwtClaims.Jti, Guid.NewGuid().ToString("N"))
        };

        claims.AddRange(roles.Select(role => new Claim(JwtClaims.Role, role)));

        // HASSAS BİLGİ KOYMA. JWT şifreli değil, sadece imzalı.
        // jwt.io'ya yapıştıran herkes içeriğini okur.

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: notBefore,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    /// Kriptografik olarak güvenli rastgele token.
    /// Guid.NewGuid() KULLANMA — tahmin edilebilirliği için tasarlanmamış olsa da
    /// bu iş için yeterli entropi garantisi vermez.
    public string CreateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public DateTime RefreshTokenExpiryUtc()
        => clock.GetUtcNow().UtcDateTime.AddDays(_settings.RefreshTokenDays);

    /// Refresh token'ın veritabanında saklanan hâli (K2).
    /// Ham token istemcide, hash'i bizde. DB dökümü sızarsa oturum ele geçirilemez.
    /// SHA-256 yeterli: girdi zaten 512 bit kriptografik rastgele — sözlük
    /// saldırısı imkânsız, bu yüzden bcrypt/PBKDF2 gibi yavaş hash gerekmiyor.
    public static string HashRefreshToken(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
