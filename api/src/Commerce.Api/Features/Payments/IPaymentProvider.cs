using Commerce.Domain.Common;

namespace Commerce.Api.Features.Payments;

public sealed record PaymentBuyer(
    string Id, string FirstName, string LastName, string Email,
    string Phone, string City, string Country, string Address,
    string Ip);                                   // fraud skoru için gerçek istemci IP'si

public sealed record PaymentBasketItem(
    string Id, string Name, string Category, decimal Price);

/// INVARIANT: Items.Sum(i => i.Price) == Amount. PaymentService garanti eder
/// (AmountAllocator ile) — iyzico sepet toplamının tutara eşit olmasını zorunlu
/// tutar, aksi hâlde isteği reddeder.
public sealed record PaymentRequest(
    string OrderNumber,
    decimal Amount,
    PaymentBuyer Buyer,
    IReadOnlyList<PaymentBasketItem> Items,
    string CallbackUrl);

public sealed record PaymentInitializationResult(
    bool Success,
    string? ProviderReference,
    /// iframe içine gömülecek HTML/script. Frontend bunu render eder.
    string? CheckoutContent,
    string? ErrorMessage,
    string RawResponse);

public sealed record PaymentVerificationResult(
    PaymentStatus Status,
    string? ProviderTransactionId,
    decimal PaidAmount,
    string? ErrorMessage,
    string RawResponse)
{
    public bool IsPaid => Status == PaymentStatus.Succeeded;
}

/// Faz E'de başka bir sağlayıcıya geçmek istersen tek sınıf yazarsın.
/// Testler ve geliştirme FakePaymentProvider ile yürür — sandbox anahtarı
/// olmadan da tüm akışı çalıştırabilirsin.
public interface IPaymentProvider
{
    string Name { get; }

    Task<PaymentInitializationResult> InitializeAsync(
        PaymentRequest request, CancellationToken ct = default);

    /// Callback'e gelen veriye GÜVENME — bunu çağırıp sağlayıcıya sor.
    Task<PaymentVerificationResult> VerifyAsync(
        string providerReference, CancellationToken ct = default);
}
