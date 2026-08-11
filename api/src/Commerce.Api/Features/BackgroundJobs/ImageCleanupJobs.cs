using Commerce.Api.Common.Images;
using Commerce.Api.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Api.Features.BackgroundJobs;

/// Soft-delete edilmiş ürünlerin Cloudinary'de barınan görsellerini siler.
/// Ürün HARD-DELETE edilmişse ProductImage satırı FK cascade ile gitmiş olur
/// ve public_id veritabanından tümüyle kaybolur — o varlıkları bu job
/// BULAMAZ (kapsam sınırı; Cloudinary Admin API taraması gerekir, bu fazın
/// dışında). Tek gerçek tetikleyicisi `dotnet run -- import` (temizle-yeniden
/// yükle) — CatalogImporter'daki uyarı log'u bunu görünür kılıyor.
public sealed class ImageCleanupJobs(
    AppDbContext db, IImageStorage storage, ILogger<ImageCleanupJobs> logger)
{
    [AutomaticRetry(Attempts = 1)]
    public async Task CleanupOrphanedImagesAsync()
    {
        // IgnoreQueryFilters ŞART: ProductImage'ın filtresi
        // (Product.DeletedAt == null) olmadan bu sorgu HİÇBİR ZAMAN satır
        // döndürmez — soft-delete edilmiş ürünün görselleri filtresiz sorguda
        // görünmez değil, görünür; filtreli sorguda hiç görünmez.
        var orphaned = await db.ProductImages
            .IgnoreQueryFilters()
            .Where(i => i.IsMigrated && i.CloudinaryPublicId != null && i.Product.DeletedAt != null)
            .Select(i => new { i.Id, i.CloudinaryPublicId })
            .ToListAsync();

        var deleted = 0;

        foreach (var image in orphaned)
        {
            // DeleteAsync istisna atmaz, false döner — job da atmaz
            // (haftalık iş için ısrar anlamsız, gelecek hafta yeniden dener).
            var ok = await storage.DeleteAsync(image.CloudinaryPublicId!);
            if (!ok) continue;

            // Damgalama ŞART (S12): yoksa aynı public_id her hafta yeniden
            // silinmeye çalışılır (Cloudinary "not found" döner, log şişer).
            // Satır SİLİNMİYOR, alanları temizleniyor — ürün geri açılırsa
            // SourceUrl hâlâ orada durur.
            await db.ProductImages
                .IgnoreQueryFilters()
                .Where(i => i.Id == image.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.IsMigrated, false)
                    .SetProperty(i => i.CloudinaryPublicId, (string?)null));

            deleted++;
        }

        logger.LogInformation(
            "Yetim görsel temizliği: {Found} bulundu, {Deleted} silindi.", orphaned.Count, deleted);
    }
}
