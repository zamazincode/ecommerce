using System.Globalization;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;

namespace Commerce.Api.Common.Images;

/// SINGLETON OLMAK ZORUNDA — her `Cloudinary` örneği KENDİ `HttpClient`'ını
/// açıyor (ölçüldü, plan 2.2). Scoped kayıt istek başına yeni HttpClient,
/// yani soket tükenmesi demek.
public sealed class CloudinaryImageStorage : IImageStorage
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryImageStorage> _logger;

    public CloudinaryImageStorage(
        IOptions<CloudinarySettings> options, ILogger<CloudinaryImageStorage> logger)
    {
        var s = options.Value;
        if (string.IsNullOrWhiteSpace(s.CloudName) ||
            string.IsNullOrWhiteSpace(s.ApiKey) ||
            string.IsNullOrWhiteSpace(s.ApiSecret))
        {
            // Bu sınıf yalnızca anahtar VARKEN kaydediliyor (Program.cs); yine de savunma.
            throw new InvalidOperationException(
                "Cloudinary:CloudName / ApiKey / ApiSecret eksik. CloudinaryImageStorage " +
                "yalnızca tam yapılandırma varken kullanılabilir.");
        }

        _logger = logger;
        _cloudinary = new Cloudinary(new Account(s.CloudName, s.ApiKey, s.ApiSecret));
        _cloudinary.Api.Secure = true;                          // Secure bir FIELD (ölçüldü)
        _cloudinary.Api.Client.Timeout = TimeSpan.FromSeconds(15);
        // Api.Timeout ATAMASI İŞE YARAMIYOR (ölçüldü): HttpClient.Timeout 100 sn kalıyor.
        // Doğrudan Api.Client.Timeout'u değiştirmek gerekiyor — Iyzipay'in .WaitAsync
        // tuzağının aynı ailesi.
    }

    public SignedUploadParams GetSignedUploadParams()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);

        // SortedDictionary: SignParameters imzayı ALFABETİK sıraya göre kurar,
        // istemcinin göndereceği alanlarla imzalanan alanlar BİREBİR aynı olmalı.
        var parameters = new SortedDictionary<string, object>
        {
            ["folder"] = ProductImageUrls.Folder,   // sunucuda sabit (K6) — istemci seçemez
            ["timestamp"] = timestamp
        };
        var signature = _cloudinary.Api.SignParameters(parameters);

        // Url elle kuruluyor: Api.ApiUrlImgUpV.BuildUrl() BİREBİR AYNI dizeyi
        // üretiyor (ölçüldü) ama Url nesnesi mutable ve zincirleme çağrılar onu
        // değiştiriyor — paylaşılan singleton'da sürpriz üretebilir.
        return new SignedUploadParams(
            Url: $"https://api.cloudinary.com/v1_1/{_cloudinary.Api.Account.Cloud}/image/upload",
            ApiKey: _cloudinary.Api.Account.ApiKey,      // public anahtar, dışarı çıkması normal
            Timestamp: timestamp,
            Signature: signature,
            Folder: ProductImageUrls.Folder);
    }

    public async Task<bool> DeleteAsync(string publicId, CancellationToken ct = default)
    {
        try
        {
            // DestroyAsync'in CancellationToken aşırı yüklemesi YOK (ölçüldü) ve
            // HttpClient varsayılanı 100 sn — Iyzipay tuzağının aynısı.
            var result = await _cloudinary.DestroyAsync(new DeletionParams(publicId))
                .WaitAsync(TimeSpan.FromSeconds(15), ct);

            // "not found" = varlık zaten yok; çağıranın satırı temizlemesini engellemeyelim.
            if (result.Result is "ok" or "not found") return true;

            _logger.LogWarning(
                "Cloudinary silme başarısız. PublicId: {PublicId}, Sonuç: {Result}",
                publicId, result.Result);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cloudinary silme sırasında hata. PublicId: {PublicId}", publicId);
            return false;
        }
    }
}
