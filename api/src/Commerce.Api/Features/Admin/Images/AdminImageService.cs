using Commerce.Api.Common.Caching;
using Commerce.Api.Common.Exceptions;
using Commerce.Api.Common.Images;
using Commerce.Api.Features.Admin.Images.Dtos;
using Commerce.Api.Persistence;
using Commerce.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Commerce.Api.Features.Admin.Images;

public sealed class AdminImageService(
    AppDbContext db, IImageStorage storage, ProductImageUrls urls,
    HybridCache cache, ILogger<AdminImageService> logger)
{
    public SignedUploadDto GetSignature()
    {
        var signed = storage.GetSignedUploadParams();
        return new SignedUploadDto(signed.Url, signed.ApiKey, signed.Timestamp, signed.Signature, signed.Folder);
    }

    public async Task<IReadOnlyList<ProductImageDto>> ListAsync(
        int productId, CancellationToken ct = default)
        => await db.ProductImages.AsNoTracking()
            .Where(i => i.ProductId == productId)
            .OrderBy(i => i.DisplayOrder)
            // DİKKAT: urls.Resolve(...)/urls.Build(...) burada ÇAĞRILAMAZ — EF
            // bir metot çağrısını SQL'e çeviremez (EffectivePrice tuzağının
            // aynı ailesi, ProductService.ToListDto'daki desenin aynısı).
            // Yalnızca hazır önek (DetailPrefix) düz `+` ile birleştirilir.
            .Select(i => new ProductImageDto(
                i.Id, i.ProductId,
                i.IsMigrated && i.CloudinaryPublicId != null
                    ? urls.DetailPrefix + i.CloudinaryPublicId
                    : i.SourceUrl,
                i.DisplayOrder, i.IsMigrated))
            .ToListAsync(ct);

    public async Task<ProductImageDto> AddAsync(
        int productId, AddProductImageRequest body, CancellationToken ct = default)
    {
        // IsActive'e BAKMA: pasif ürüne de görsel eklenebilmeli (admin ürünü
        // yayına almadan önce kapağını hazırlıyor olabilir).
        var exists = await db.Products.AnyAsync(p => p.Id == productId, ct);
        if (!exists) throw NotFoundException.For("Ürün", productId);

        int order;
        if (body.DisplayOrder is { } requested)
        {
            order = requested;
        }
        else
        {
            var maxExisting = await db.ProductImages
                .Where(i => i.ProductId == productId)
                .Select(i => (int?)i.DisplayOrder)
                .MaxAsync(ct);
            order = maxExisting is { } max ? max + 1 : 0;
        }

        var image = new ProductImage
        {
            ProductId = productId,
            CloudinaryPublicId = body.PublicId,
            IsMigrated = true,
            // SourceUrl NOT NULL — dönüşümsüz Cloudinary URL'i hem geçerli hem anlamlı.
            SourceUrl = urls.Build(body.PublicId, ImageTransformation.Original),
            DisplayOrder = order
        };

        db.ProductImages.Add(image);
        await db.SaveChangesAsync(ct);

        // Ürün detayı 10 dk, ana sayfa 15 dk cache'li — bu olmadan admin'in
        // eklediği görsel dakikalarca görünmez (PLAN.md'nin "en sinsi hata"sı).
        await cache.RemoveByTagAsync(CacheTags.Products, ct);

        return new ProductImageDto(
            image.Id, image.ProductId,
            urls.Build(body.PublicId, ImageTransformation.Detail),
            image.DisplayOrder, Hosted: true);
    }

    public async Task DeleteAsync(int imageId, CancellationToken ct = default)
    {
        // IgnoreQueryFilters: silinmiş ürünün görseli de kaldırılabilsin —
        // aksi hâlde soft-delete edilmiş ürüne ait yetim bir satır asla temizlenemez.
        var image = await db.ProductImages.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == imageId, ct)
            ?? throw NotFoundException.For("Görsel", imageId);

        if (image.CloudinaryPublicId is { } publicId)
        {
            var deleted = await storage.DeleteAsync(publicId, ct);
            if (!deleted)
                // Kalan varlık kotayı yer, yetim job'ı onu bulamaz (satır zaten gidiyor)
                // — ama ürün sayfası bozuk görsel göstermesin diye satır yine de silinir.
                logger.LogWarning(
                    "Cloudinary'den silinemedi ama satır kaldırılıyor. PublicId: {PublicId}", publicId);
        }

        db.ProductImages.Remove(image);
        await db.SaveChangesAsync(ct);

        await cache.RemoveByTagAsync(CacheTags.Products, ct);
    }
}
