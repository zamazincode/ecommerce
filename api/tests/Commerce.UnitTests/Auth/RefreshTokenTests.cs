using Commerce.Domain.Users;
using Shouldly;

namespace Commerce.UnitTests.Auth;

/// Commerce.Domain saf POCO — ASP.NET'e bağımlı değil, bu yüzden testi de
/// burada (Commerce.UnitTests), CLAUDE.md'deki mimari kuralın gereği.
public class RefreshTokenTests
{
    private static readonly DateTime ExpiresAt = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(-1, false)]  // süresinden 1 saniye önce → geçerli
    [InlineData(0, true)]    // tam süre anında → dolmuş sayılır (kapsayıcı sınır)
    [InlineData(1, true)]    // süresinden 1 saniye sonra → dolmuş
    public void IsExpired_IsInclusiveAtExpiryInstant(int secondsOffset, bool expected)
    {
        var token = new RefreshToken { ExpiresAt = ExpiresAt };
        var now = ExpiresAt.AddSeconds(secondsOffset);

        token.IsExpired(now).ShouldBe(expected);
    }

    [Fact]
    public void IsRevoked_FollowsRevokedAt()
    {
        var active = new RefreshToken { RevokedAt = null };
        var revoked = new RefreshToken { RevokedAt = DateTime.UtcNow };

        active.IsRevoked.ShouldBeFalse();
        revoked.IsRevoked.ShouldBeTrue();
    }

    [Fact]
    public void IsActive_FalseWhenRevoked_EvenIfNotExpired()
    {
        var token = new RefreshToken
        {
            ExpiresAt = ExpiresAt,
            RevokedAt = ExpiresAt.AddDays(-1)
        };

        token.IsActive(ExpiresAt.AddDays(-2)).ShouldBeFalse();
    }

    [Fact]
    public void IsActive_TrueOnlyWhenNeitherRevokedNorExpired()
    {
        var token = new RefreshToken { ExpiresAt = ExpiresAt, RevokedAt = null };

        token.IsActive(ExpiresAt.AddDays(-1)).ShouldBeTrue();
    }
}
