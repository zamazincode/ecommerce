using System.Collections.Concurrent;
using System.Text.Json;
using Commerce.Domain.Common;

namespace Commerce.Api.Features.Payments;

/// Geliştirme ve test için. Gerçek bir HTTP çağrısı yapmaz.
/// Davranış testlerin ayarladığı bayraklarla belirlenir — "1 TL biten tutar
/// başarısız olur" gibi örtük kurallar YOK, hepsi açık.
public sealed class FakePaymentProvider : IPaymentProvider
{
    // Referans → isteği eşleştirir. Kılavuzun FirstOrDefault(r => providerReference
    // .Contains("fake-")) lambda'sı r'yi hiç kullanmıyordu, her zaman ilk isteği
    // döndürüyordu — ikinci sipariş için PaidAmount başka siparişin tutarı olurdu.
    private readonly ConcurrentDictionary<string, PaymentRequest> _byReference = new();
    private readonly ConcurrentQueue<PaymentRequest> _initialized = new();
    private int _verifyCallCount;

    public string Name => "fake";

    /// Testlerin davranışı yönlendirdiği anahtarlar.
    public PaymentStatus? NextResultOverride { get; set; }
    public decimal? PaidAmountOverride { get; set; }   // tutar uyuşmazlığı senaryosu
    public bool FailInitialize { get; set; }

    /// "Sağlayıcıya sorulmadı" iddiaları için (K2 kısa devresi).
    public int VerifyCallCount => _verifyCallCount;
    public IReadOnlyList<PaymentRequest> InitializedRequests => [.. _initialized];

    public Task<PaymentInitializationResult> InitializeAsync(
        PaymentRequest request, CancellationToken ct = default)
    {
        _initialized.Enqueue(request);

        if (FailInitialize)
        {
            return Task.FromResult(new PaymentInitializationResult(
                Success: false,
                ProviderReference: null,
                CheckoutContent: null,
                ErrorMessage: "Sahte sağlayıcı: başlatma reddedildi.",
                RawResponse: JsonSerializer.Serialize(new { status = "failure" })));
        }

        var reference = $"fake-{Guid.NewGuid():N}";
        _byReference[reference] = request;

        return Task.FromResult(new PaymentInitializationResult(
            Success: true,
            ProviderReference: reference,
            CheckoutContent: $"<div id='fake-checkout' data-token='{reference}'>Sahte ödeme formu</div>",
            ErrorMessage: null,
            RawResponse: JsonSerializer.Serialize(new { token = reference, status = "success" })));
    }

    public Task<PaymentVerificationResult> VerifyAsync(
        string providerReference, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _verifyCallCount);

        _byReference.TryGetValue(providerReference, out var request);
        var status = NextResultOverride ?? PaymentStatus.Succeeded;

        var result = status == PaymentStatus.Succeeded
            ? new PaymentVerificationResult(
                PaymentStatus.Succeeded,
                ProviderTransactionId: $"txn-{providerReference}",
                PaidAmount: PaidAmountOverride ?? request?.Amount ?? 0m,
                ErrorMessage: null,
                RawResponse: JsonSerializer.Serialize(new { paymentStatus = "SUCCESS" }))
            : new PaymentVerificationResult(
                PaymentStatus.Failed,
                ProviderTransactionId: null,
                PaidAmount: 0m,
                ErrorMessage: "Sahte sağlayıcı: ödeme reddedildi.",
                RawResponse: JsonSerializer.Serialize(new { paymentStatus = "FAILURE" }));

        return Task.FromResult(result);
    }
}
