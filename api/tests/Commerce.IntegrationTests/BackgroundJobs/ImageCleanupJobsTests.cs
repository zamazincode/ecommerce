using Commerce.Api.Features.BackgroundJobs;
using Commerce.Domain.Catalog;
using Commerce.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Commerce.IntegrationTests.BackgroundJobs;

public class ImageCleanupJobsTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task RunCleanupAsync()
        => ExecuteScopedAsync(sp =>
            sp.GetRequiredService<ImageCleanupJobs>().CleanupOrphanedImagesAsync());

    /// Cloudinary'de barınan bir görsele sahip, SOFT-DELETE edilmiş bir ürün kurar.
    private async Task<int> SeedSoftDeletedProductWithHostedImageAsync(string publicId = "products/silinecek")
    {
        var id = 0;
        await ExecuteDbAsync(async db =>
        {
            var product = new ProductBuilder().WithCloudinaryImage(publicId).Build();
            db.Products.Add(product);
            await db.SaveChangesAsync(Ct);

            // IgnoreQueryFilters gerekmiyor: henüz silinmedi, sonra ExecuteUpdateAsync
            // ile DeletedAt damgalanacak.
            id = product.Id;
        });

        await ExecuteDbAsync(db => db.Products
            .Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.DeletedAt, DateTime.UtcNow), Ct));

        return id;
    }

    private Task<ProductImage> LoadImageAsync(int productId)
        => ExecuteDbAsync(db => db.ProductImages
            .IgnoreQueryFilters()
            .SingleAsync(i => i.ProductId == productId, Ct));

    [Fact]
    public async Task Cleanup_DeletesHostedImageOfSoftDeletedProduct()
    {
        await SeedSoftDeletedProductWithHostedImageAsync("products/silinecek-1");

        await RunCleanupAsync();

        Factory.ImageStorage.DeletedPublicIds.ShouldContain("products/silinecek-1");
    }

    [Fact]
    public async Task Cleanup_ClearsRowSoSecondRunDoesNothing()
    {
        var productId = await SeedSoftDeletedProductWithHostedImageAsync("products/silinecek-2");

        await RunCleanupAsync();
        await RunCleanupAsync();

        // S12 kilidi: damgalama yapılmasaydı ikinci çalıştırma da silmeye çalışırdı.
        Factory.ImageStorage.DeletedPublicIds.Count(id => id == "products/silinecek-2").ShouldBe(1);

        var image = await LoadImageAsync(productId);
        image.IsMigrated.ShouldBeFalse();
        image.CloudinaryPublicId.ShouldBeNull();
    }

    [Fact]
    public async Task Cleanup_LeavesLiveProductsAlone()
    {
        await ExecuteDbAsync(async db =>
        {
            db.Products.Add(new ProductBuilder().WithCloudinaryImage("products/canli").Build());
            await db.SaveChangesAsync(Ct);
        });

        await RunCleanupAsync();

        Factory.ImageStorage.DeletedPublicIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cleanup_IgnoresNonHostedImages()
    {
        var id = 0;
        await ExecuteDbAsync(async db =>
        {
            // Varsayılan ProductBuilder D&R kaynaklı (IsMigrated = false) bir görsel kurar.
            var product = new ProductBuilder().Build();
            db.Products.Add(product);
            await db.SaveChangesAsync(Ct);
            id = product.Id;
        });

        await ExecuteDbAsync(db => db.Products
            .Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.DeletedAt, DateTime.UtcNow), Ct));

        await RunCleanupAsync();

        Factory.ImageStorage.DeletedPublicIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cleanup_WhenStorageFails_KeepsPublicIdForRetry()
    {
        var productId = await SeedSoftDeletedProductWithHostedImageAsync("products/silinecek-3");
        Factory.ImageStorage.DeleteShouldFail = true;

        await Should.NotThrowAsync(RunCleanupAsync);

        var image = await LoadImageAsync(productId);
        image.IsMigrated.ShouldBeTrue();
        image.CloudinaryPublicId.ShouldBe("products/silinecek-3");
    }
}
