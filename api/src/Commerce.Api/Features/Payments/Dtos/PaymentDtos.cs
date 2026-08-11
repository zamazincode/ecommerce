using Commerce.Domain.Common;

namespace Commerce.Api.Features.Payments.Dtos;

public sealed record InitializePaymentRequest(string OrderNumber);

public sealed record PaymentInitializedDto(
    string OrderNumber, decimal Amount, string ProviderReference, string CheckoutContent);

public sealed record PaymentStatusDto(
    string OrderNumber, OrderStatus OrderStatus, PaymentStatus? PaymentStatus,
    decimal Amount, DateTime? UpdatedAt);          // "CompletedAt" yanıltıcı olurdu:
                                                   // başarısız denemede de doluyor

/// Callback/webhook'un sonucu — endpoint bunu yönlendirme parametresine çevirir.
public enum PaymentCompletionOutcome { Succeeded, Failed, AmountMismatch, UnknownReference }

public sealed record PaymentCompletionResult(
    PaymentCompletionOutcome Outcome, PaymentStatusDto? Status);
