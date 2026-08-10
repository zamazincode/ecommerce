using System.Text.Json;

namespace Commerce.Api.Features.Payments;

/// Callback tarayıcıdan FORM, webhook iyzico sunucusundan JSON gelir.
/// Hiçbir koşulda exception atmaz; okuyamazsa null döner — böylece iyzico'nun
/// gövde biçimini değiştirmesi 500'e değil, loglanan bir null'a dönüşür.
internal static class PaymentCallbackReader
{
    public static async Task<string?> TryReadTokenAsync(HttpRequest request, CancellationToken ct)
    {
        try
        {
            if (request.HasFormContentType)
            {
                var form = await request.ReadFormAsync(ct);
                var token = form["token"].ToString();
                return string.IsNullOrWhiteSpace(token) ? null : token;
            }

            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct);
            return document.RootElement.TryGetProperty("token", out var tokenElement)
                ? tokenElement.GetString()
                : null;
        }
        catch (Exception)
        {
            // Bozuk gövde, boş gövde, beklenmeyen içerik türü — hepsi "token yok"
            // sayılır. Webhook endpoint'i bu sayede sağlayıcıya her zaman 200 döner.
            return null;
        }
    }
}
