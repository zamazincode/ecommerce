using System.IdentityModel.Tokens.Jwt;
using Commerce.Api.Features.Auth;
using Commerce.Api.Persistence.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Commerce.UnitTests.Auth;

public class TokenServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    private static (TokenService Service, FakeTimeProvider Clock) CreateService()
    {
        var clock = new FakeTimeProvider(Now);
        var settings = Options.Create(new JwtSettings
        {
            Key = "bu-test-anahtari-en-az-32-karakter-uzunlugunda",
            Issuer = "commerce-api",
            Audience = "commerce-clients",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 30
        });
        return (new TokenService(settings, clock), clock);
    }

    private static ApplicationUser SampleUser() => new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Email = "ali@test.com",
        UserName = "ali@test.com"
    };

    [Fact]
    public void CreateAccessToken_ContainsExpectedClaims()
    {
        // Arrange
        var (service, _) = CreateService();
        var user = SampleUser();

        // Act
        var (token, _) = service.CreateAccessToken(user, ["Customer", "Admin"]);

        // Assert
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.First(c => c.Type == JwtClaims.Sub).Value.ShouldBe(user.Id.ToString());
        jwt.Claims.First(c => c.Type == JwtClaims.Email).Value.ShouldBe("ali@test.com");
        jwt.Claims.Count(c => c.Type == JwtClaims.Role).ShouldBe(2);
        jwt.Claims.ShouldContain(c => c.Type == JwtClaims.Jti);
        jwt.Issuer.ShouldBe("commerce-api");
    }

    [Fact]
    public void CreateAccessToken_DoesNotLeakSensitiveData()
    {
        var (service, _) = CreateService();
        var user = SampleUser();
        user.PasswordHash = "COK-GIZLI-HASH";

        var (token, _) = service.CreateAccessToken(user, ["Customer"]);

        // JWT şifreli değil — içine hassas bir şey koymadığımızı doğruluyoruz.
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.ShouldNotContain(c => c.Value.Contains("COK-GIZLI-HASH"));
    }

    [Fact]
    public void CreateAccessToken_ExpiresAfterConfiguredLifetime()
    {
        var (service, _) = CreateService();

        var (_, expiresAt) = service.CreateAccessToken(SampleUser(), []);

        expiresAt.ShouldBe(Now.UtcDateTime.AddMinutes(15));
    }

    [Fact]
    public void RefreshTokenExpiry_UsesInjectedClock()
    {
        var (service, clock) = CreateService();

        clock.Advance(TimeSpan.FromDays(10));

        // DateTime.UtcNow kullansaydık bu test geçmezdi.
        service.RefreshTokenExpiryUtc()
               .ShouldBe(Now.UtcDateTime.AddDays(10).AddDays(30));
    }

    [Fact]
    public void CreateRefreshToken_ProducesUniqueHighEntropyValues()
    {
        var (service, _) = CreateService();

        var tokens = Enumerable.Range(0, 1000).Select(_ => service.CreateRefreshToken()).ToList();

        tokens.Distinct().Count().ShouldBe(1000);
        // 64 byte → Base64'te 88 karakter
        tokens.ShouldAllBe(t => t.Length >= 80);
    }

    [Fact]
    public void HashRefreshToken_IsDeterministicAndDiffersFromRawToken()
    {
        const string raw = "ayni-girdi-ayni-cikti";

        var hash1 = TokenService.HashRefreshToken(raw);
        var hash2 = TokenService.HashRefreshToken(raw);
        var otherHash = TokenService.HashRefreshToken("farkli-girdi");

        // SHA-256 hex çıktısı her zaman 64 karakter.
        hash1.Length.ShouldBe(64);
        hash1.ShouldBe(hash2);
        hash1.ShouldNotBe(raw);
        hash1.ShouldNotBe(otherHash);
    }
}
