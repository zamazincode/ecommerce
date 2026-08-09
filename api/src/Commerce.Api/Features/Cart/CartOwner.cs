using System.Security.Claims;
using Commerce.Api.Common.Exceptions;
using Commerce.Api.Common.Extensions;
using Microsoft.Net.Http.Headers;

namespace Commerce.Api.Features.Cart;

/// Sepetin kime ait olduğu. İkisi birden dolu olmaz.
public readonly record struct CartOwner(Guid? UserId, string? GuestId)
{
    public const string GuestHeader = "X-Guest-Id";

    public bool IsMember => UserId.HasValue;

    public static CartOwner Resolve(ClaimsPrincipal user, HttpRequest request)
    {
        var userId = user.GetUserIdOrNull();
        if (userId.HasValue) return new CartOwner(userId, null);

        // Authorization başlığı VAR ama kimlik doğrulanmamış: token bozuk ya da
        // süresi geçmiş. Sessizce misafir sepetine düşersek kullanıcı 401 yerine
        // BOŞ BİR SEPET görür ve "sepetim silindi" der (ölçüm 2.5).
        if (request.Headers.ContainsKey(HeaderNames.Authorization))
            throw new UnauthorizedException(
                "Oturumunuzun süresi dolmuş. Lütfen tekrar giriş yapın.");

        return new CartOwner(null, ParseGuestId(request));
    }

    /// X-Guest-Id GUID OLMAK ZORUNDA: Redis anahtarının uzunluğunu ve
    /// içeriğini istemciye bırakmıyoruz.
    public static string ParseGuestId(HttpRequest request)
    {
        var raw = request.Headers[GuestHeader].ToString();
        if (string.IsNullOrWhiteSpace(raw) || !Guid.TryParse(raw, out var id))
            throw new BusinessRuleException(
                $"Misafir sepeti için geçerli bir {GuestHeader} başlığı gönderilmeli.");

        return id.ToString();   // normalize: büyük/küçük harf ve süslü parantez farkı anahtarı bölmesin
    }
}
