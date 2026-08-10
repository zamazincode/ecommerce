using System.Security.Claims;
using Commerce.Api.Common.Extensions;
using Commerce.Api.Common.Filters;
using Commerce.Api.Features.Auth;
using Commerce.Api.Features.Payments.Dtos;
using Microsoft.Extensions.Options;

namespace Commerce.Api.Features.Payments;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments").WithTags("Payments");

        group.MapPost("/initialize", Initialize)
             .RequireAuthorization()
             .RequireRateLimiting("payments")
             .WithValidation<InitializePaymentRequest>()
             .WithSummary("Ödeme başlatır, iframe içeriği döner")
             .Produces<PaymentInitializedDto>()
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status403Forbidden)
             .ProducesProblem(StatusCodes.Status404NotFound);

        // ANONİM — iyzico'nun sunucusu/tarayıcı çağırıyor, token'ımız yok.
        // Güvenlik kimlik doğrulamadan DEĞİL, VerifyAsync'in sağlayıcıya geri
        // sormasından geliyor (K2). DisableRateLimiting YAZILMIYOR: global
        // limiter (IP başına 200/dk) açık kalsın, sahte POST seli iyzico'ya
        // giden HTTPS isteğine dönüşmesin.
        group.MapPost("/callback", Callback)
             .AllowAnonymous()
             .WithSummary("3D Secure dönüşü — iyzico çağırır")
             .ExcludeFromDescription();

        group.MapPost("/webhook", Webhook)
             .AllowAnonymous()
             .WithSummary("Asenkron bildirim — iyzico çağırır")
             .ExcludeFromDescription();

        group.MapGet("/{orderNumber}/status", GetStatus)
             .RequireAuthorization()
             .RequireRateLimiting("payments")
             .WithSummary("Siparişin ödeme durumu")
             .Produces<PaymentStatusDto>()
             .ProducesProblem(StatusCodes.Status403Forbidden)
             .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<PaymentInitializedDto> Initialize(
        InitializePaymentRequest body, ClaimsPrincipal user, HttpContext context,
        PaymentService service, CancellationToken ct)
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        return await service.InitializeAsync(user.GetUserId(), body.OrderNumber, clientIp, ct);
    }

    /// iyzico bu uca form-encoded POST gönderir; kullanıcının tarayıcısı
    /// buraya yönlendirildiği için sonunda frontend'e redirect ediyoruz.
    /// CompleteAsync bilinen hiçbir yolda exception atmaz ama yine de
    /// try/catch ile sarılıyor: beklenmedik bir hata 3DS dönüşünde kullanıcının
    /// yüzüne ham ProblemDetails JSON'u basmasın.
    private static async Task<IResult> Callback(
        HttpRequest request, PaymentService service, IOptions<WebAppSettings> webAppSettings,
        ILogger<PaymentService> logger, CancellationToken ct)
    {
        var baseUrl = webAppSettings.Value.BaseUrl.TrimEnd('/');
        var token = await PaymentCallbackReader.TryReadTokenAsync(request, ct);

        if (token is null)
            return TypedResults.Redirect($"{baseUrl}/odeme/sonuc?durum=gecersiz");

        try
        {
            var result = await service.CompleteAsync(token, ct);

            var durum = result.Outcome switch
            {
                PaymentCompletionOutcome.Succeeded => "basarili",
                PaymentCompletionOutcome.Failed => "basarisiz",
                PaymentCompletionOutcome.AmountMismatch => "inceleniyor",
                _ => "gecersiz"
            };
            var siparis = result.Status is null
                ? ""
                : $"&siparis={Uri.EscapeDataString(result.Status.OrderNumber)}";

            return TypedResults.Redirect($"{baseUrl}/odeme/sonuc?durum={durum}{siparis}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Callback işlenirken beklenmeyen hata. Token: {Token}", token);
            return TypedResults.Redirect($"{baseUrl}/odeme/sonuc?durum=hata");
        }
    }

    /// Webhook sunucudan sunucuya gelir — redirect yok, HER KOŞULDA 200.
    /// Sağlayıcı 200 alamazsa saatlerce tekrar dener; exception LogError ile
    /// yutulur.
    private static async Task<IResult> Webhook(
        HttpRequest request, PaymentService service, ILogger<PaymentService> logger, CancellationToken ct)
    {
        var token = await PaymentCallbackReader.TryReadTokenAsync(request, ct);

        if (token is null)
        {
            logger.LogWarning("Webhook gövdesinden token okunamadı.");
            return TypedResults.Ok();
        }

        try
        {
            await service.CompleteAsync(token, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Webhook işlenemedi. Token: {Token}", token);
        }

        return TypedResults.Ok();
    }

    private static async Task<PaymentStatusDto> GetStatus(
        string orderNumber, ClaimsPrincipal user, PaymentService service, CancellationToken ct)
        => await service.GetStatusAsync(user.GetUserId(), orderNumber, user.IsAdmin(), ct);
}
