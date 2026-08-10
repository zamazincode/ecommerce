using System.Text.Json;
using Commerce.Domain.Common;
using Commerce.Domain.Pricing;
using Iyzipay;
using Iyzipay.Model;
using Iyzipay.Request;
using Microsoft.Extensions.Options;

// TUZAK: Iyzipay.Model'de de Commerce.Domain.Orders'da da bir "Payment" tipi
// var. Bu dosya BİLEREK Commerce.Domain.Orders'ı kullanmıyor (CS0104 önlenir);
// yalnızca Commerce.Domain.Common (PaymentStatus) ve Commerce.Domain.Pricing.
namespace Commerce.Api.Features.Payments;

public sealed class IyzicoPaymentProvider(
    IOptions<IyzicoSettings> settings,
    ILogger<IyzicoPaymentProvider> logger) : IPaymentProvider
{
    // Iyzipay CancellationToken almıyor, static HttpClient varsayılan 100 sn
    // bekliyor. İsteğimizi 30 sn'de kesip kullanıcıyı asılı bırakmıyoruz —
    // alttaki istek arka planda devam eder ama bize göre "başarısız" sayılır.
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(30);

    private readonly IyzicoSettings _settings = settings.Value;

    public string Name => "iyzico";

    // Tam nitelikli: Microsoft.Extensions.Options.Options statik sınıfıyla
    // aynı ada sahip (CS0104).
    private Iyzipay.Options BuildOptions() => new()
    {
        ApiKey = _settings.ApiKey,
        SecretKey = _settings.SecretKey,
        BaseUrl = _settings.BaseUrl
    };

    public async Task<PaymentInitializationResult> InitializeAsync(
        PaymentRequest request, CancellationToken ct = default)
    {
        // TUTAR BİÇİMİ: nokta ayıraçlı metin. Kültürden bağımsız.
        var amount = MoneyUnits.ToProviderString(request.Amount);

        var iyzicoRequest = new CreateCheckoutFormInitializeRequest
        {
            Locale = Locale.TR.ToString(),
            // conversationId: kendi tarafımızdaki korelasyon kimliği.
            // Log'larda ve destek taleplerinde bunu kullanacağız.
            ConversationId = request.OrderNumber,
            Price = amount,
            PaidPrice = amount,
            Currency = Currency.TRY.ToString(),
            BasketId = request.OrderNumber,
            PaymentGroup = PaymentGroup.PRODUCT.ToString(),
            CallbackUrl = request.CallbackUrl,
            // Taksit kapalı: PaidPrice'ın Price'tan sapması imkânsız hâle gelir,
            // tutar doğrulaması katı eşitlikle yapılabilir.
            EnabledInstallments = [1],
            Buyer = new Buyer
            {
                Id = request.Buyer.Id,
                Name = request.Buyer.FirstName,
                Surname = request.Buyer.LastName,
                Email = request.Buyer.Email,
                GsmNumber = request.Buyer.Phone,
                // iyzico sandbox'ın kabul ettiği sabit değer — üretimde gerçek
                // TCKN toplanmıyorsa bu bilinçli bir kabul.
                IdentityNumber = "11111111111",
                RegistrationAddress = request.Buyer.Address,
                City = request.Buyer.City,
                Country = request.Buyer.Country,
                Ip = request.Buyer.Ip
            },
            ShippingAddress = new Address
            {
                ContactName = $"{request.Buyer.FirstName} {request.Buyer.LastName}",
                City = request.Buyer.City,
                Country = request.Buyer.Country,
                Description = request.Buyer.Address
            },
            BillingAddress = new Address
            {
                ContactName = $"{request.Buyer.FirstName} {request.Buyer.LastName}",
                City = request.Buyer.City,
                Country = request.Buyer.Country,
                Description = request.Buyer.Address
            },
            // Sepet kalemleri toplamı Price/PaidPrice'a TAM eşit olmak zorunda
            // (iyzico zorunlu tutuyor). PaymentService bunu AmountAllocator ile
            // garanti ediyor — burada yalnızca sayı metne çevriliyor.
            BasketItems = request.Items.Select(i => new BasketItem
            {
                Id = i.Id,
                Name = i.Name,
                Category1 = i.Category,
                ItemType = BasketItemType.PHYSICAL.ToString(),
                Price = MoneyUnits.ToProviderString(i.Price)
            }).ToList()
        };

        CheckoutFormInitialize response;
        try
        {
            response = await CheckoutFormInitialize.Create(iyzicoRequest, BuildOptions())
                .WaitAsync(CallTimeout, ct);
        }
        catch (TimeoutException)
        {
            logger.LogWarning(
                "iyzico başlatma zaman aşımına uğradı. Sipariş: {OrderNumber}", request.OrderNumber);
            return new PaymentInitializationResult(
                false, null, null, "Ödeme sağlayıcısına ulaşılamadı.",
                JsonSerializer.Serialize(new { error = "timeout" }));
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex,
                "iyzico başlatma isteği başarısız. Sipariş: {OrderNumber}", request.OrderNumber);
            return new PaymentInitializationResult(
                false, null, null, "Ödeme sağlayıcısına ulaşılamadı.",
                JsonSerializer.Serialize(new { error = "http" }));
        }

        var raw = BuildInitializeRawResponse(response);

        if (response.Status != "success")
        {
            logger.LogWarning(
                "iyzico başlatma başarısız. Sipariş: {OrderNumber}, Hata: {Error}",
                request.OrderNumber, response.ErrorMessage);

            return new PaymentInitializationResult(
                false, null, null, response.ErrorMessage ?? "Ödeme başlatılamadı.", raw);
        }

        return new PaymentInitializationResult(
            Success: true,
            ProviderReference: response.Token,
            CheckoutContent: response.CheckoutFormContent,
            ErrorMessage: null,
            RawResponse: raw);
    }

    public async Task<PaymentVerificationResult> VerifyAsync(
        string providerReference, CancellationToken ct = default)
    {
        // BU METOT ATLANAMAZ.
        // Callback'e gelen veriye değil, sağlayıcının bu çağrıya verdiği
        // cevaba güveniyoruz. Saldırgan callback URL'ine sahte "başarılı"
        // isteği gönderebilir; buraya erişemez.
        CheckoutForm response;
        try
        {
            response = await CheckoutForm.Retrieve(
                new RetrieveCheckoutFormRequest { Locale = Locale.TR.ToString(), Token = providerReference },
                BuildOptions())
                .WaitAsync(CallTimeout, ct);
        }
        catch (TimeoutException)
        {
            logger.LogWarning(
                "iyzico doğrulama zaman aşımına uğradı. Referans: {Reference}", providerReference);
            return new PaymentVerificationResult(
                PaymentStatus.Failed, null, 0m, "Ödeme sağlayıcısına ulaşılamadı.",
                JsonSerializer.Serialize(new { error = "timeout" }));
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex,
                "iyzico doğrulama isteği başarısız. Referans: {Reference}", providerReference);
            return new PaymentVerificationResult(
                PaymentStatus.Failed, null, 0m, "Ödeme sağlayıcısına ulaşılamadı.",
                JsonSerializer.Serialize(new { error = "http" }));
        }

        var raw = BuildVerifyRawResponse(response);

        if (response.Status != "success" || response.PaymentStatus != "SUCCESS")
        {
            return new PaymentVerificationResult(
                PaymentStatus.Failed,
                ProviderTransactionId: null,
                PaidAmount: 0m,
                ErrorMessage: response.ErrorMessage ?? "Ödeme doğrulanamadı.",
                RawResponse: raw);
        }

        return new PaymentVerificationResult(
            PaymentStatus.Succeeded,
            ProviderTransactionId: response.PaymentId,
            PaidAmount: MoneyUnits.ParseProviderAmount(response.PaidPrice),
            ErrorMessage: null,
            RawResponse: raw);
    }

    /// CheckoutFormContent kilobaytlarca HTML/script taşır — RawResponse'a
    /// aynen yazılırsa 20.000 karakterin ortasından kesilip ayrıştırılamaz bir
    /// yarım JSON'a döner. Yalnızca tanılama alanları serileştirilir.
    private static string BuildInitializeRawResponse(CheckoutFormInitialize response)
        => JsonSerializer.Serialize(new
        {
            response.Status,
            response.StatusCode,
            response.ErrorCode,
            response.ErrorMessage,
            response.ErrorGroup,
            response.Token,
            response.ConversationId,
            response.SystemTime,
            response.Signature,
            checkoutFormContentLength = response.CheckoutFormContent?.Length ?? 0
        });

    /// cardToken/cardUserKey maskelenir — API anahtarı sızarsa kartı yeniden
    /// çekmeye yarayan değerler, loglanacak bir şey değil. binNumber ve
    /// lastFourDigits PAN olmadığı için kalır (destek için gerekli).
    private static string BuildVerifyRawResponse(CheckoutForm response)
        => JsonSerializer.Serialize(new
        {
            response.Status,
            response.StatusCode,
            response.ErrorCode,
            response.ErrorMessage,
            response.ErrorGroup,
            response.PaymentId,
            response.PaymentStatus,
            response.PaidPrice,
            response.Price,
            response.Currency,
            response.Installment,
            response.BinNumber,
            response.LastFourDigits,
            CardToken = "***",
            CardUserKey = "***",
            response.FraudStatus,
            response.MdStatus,
            response.ConversationId,
            response.SystemTime
        });
}
