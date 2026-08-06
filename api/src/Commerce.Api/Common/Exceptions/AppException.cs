namespace Commerce.Api.Common.Exceptions;

/// Aranan kayıt yok → 404
public class NotFoundException(string message) : Exception(message)
{
    public static NotFoundException For(string entity, object key)
        => new($"{entity} bulunamadı. (anahtar: {key})");
}

/// Kullanıcı giriş yapmış ama bu kayda erişemez → 403
/// DİKKAT: 404 dönme. "Yok" demek "var ama senin değil" bilgisini gizler ama
public class ForbiddenException(string message = "Bu işlem için yetkiniz yok.")
    : Exception(message);

/// İş kuralı ihlali → 400. "Stokta 2 var, 5 istediniz" gibi.
public class BusinessRuleException(string message) : Exception(message);

/// Çakışan durum → 409. "Bu e-posta zaten kayıtlı" gibi.
public class ConflictException(string message) : Exception(message);