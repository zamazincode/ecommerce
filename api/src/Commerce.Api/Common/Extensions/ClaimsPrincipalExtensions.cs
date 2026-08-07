using System.Security.Claims;
using Commerce.Api.Common.Exceptions;
using Commerce.Api.Features.Auth;

namespace Commerce.Api.Common.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// Token'daki kullanıcı kimliği. [Authorize] geçmiş bir istekte her zaman dolu.
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(JwtClaims.Sub);
        return Guid.TryParse(raw, out var id)
            ? id
            : throw new UnauthorizedException("Token içinde geçerli bir kullanıcı kimliği yok.");
    }

    public static Guid? GetUserIdOrNull(this ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(JwtClaims.Sub), out var id) ? id : null;

    public static bool IsAdmin(this ClaimsPrincipal principal)
        => principal.IsInRole(Persistence.Identity.AppRoles.Admin);
}
