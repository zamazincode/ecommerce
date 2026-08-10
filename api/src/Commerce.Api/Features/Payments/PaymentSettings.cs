namespace Commerce.Api.Features.Payments;

/// Sağlayıcıdan BAĞIMSIZ ayar. PaymentService hem Fake hem Iyzico ile çalışıyor;
/// CallbackUrl'ü IyzicoSettings'e koymak Testing/Development'ta (Iyzico bölümü
/// boşken) sessizce null'a düşerdi.
public sealed class PaymentSettings
{
    public const string SectionName = "Payments";
    public string CallbackUrl { get; init; } = "http://localhost:5000/api/payments/callback";
}

public sealed class IyzicoSettings
{
    public const string SectionName = "Iyzico";

    public string ApiKey { get; init; } = null!;
    public string SecretKey { get; init; } = null!;
    public string BaseUrl { get; init; } = "https://sandbox-api.iyzipay.com";
}
