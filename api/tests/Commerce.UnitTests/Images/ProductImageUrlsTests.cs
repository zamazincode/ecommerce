using Commerce.Api.Common.Images;
using Commerce.Domain.Catalog;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Commerce.UnitTests.Images;

public class ProductImageUrlsTests
{
    private static ProductImageUrls Create(string cloudName = "test-cloud")
        => new(Options.Create(new CloudinarySettings { CloudName = cloudName }));

    [Fact]
    public void CardPrefix_UsesExpectedTransformation()
        => Create().CardPrefix.ShouldBe(
            "https://res.cloudinary.com/test-cloud/image/upload/w_300,h_450,c_fill,f_auto,q_auto/");

    [Fact]
    public void DetailPrefix_UsesExpectedTransformation()
        => Create().DetailPrefix.ShouldBe(
            "https://res.cloudinary.com/test-cloud/image/upload/w_600,h_900,c_fit,f_auto,q_auto/");

    [Fact]
    public void ThumbnailPrefix_UsesExpectedTransformation()
        => Create().ThumbnailPrefix.ShouldBe(
            "https://res.cloudinary.com/test-cloud/image/upload/w_80,h_120,c_fill,f_auto,q_auto/");

    [Fact]
    public void OriginalPrefix_HasNoTransformationSegment()
    {
        var prefix = Create().OriginalPrefix;

        prefix.ShouldEndWith("/image/upload/");
        prefix.ShouldNotContain("w_");
    }

    [Fact]
    public void Prefixes_ContainNoDoubleSlashAfterHost()
    {
        var urls = Create();

        foreach (var prefix in new[] { urls.CardPrefix, urls.DetailPrefix, urls.ThumbnailPrefix, urls.OriginalPrefix })
            prefix.Replace("https://", "").ShouldNotContain("//");
    }

    [Fact]
    public void Build_ProducesExpectedUrl()
    {
        var urls = Create();

        var result = urls.Build("products/abc", ImageTransformation.Card);

        result.ShouldBe(urls.CardPrefix + "products/abc");
    }

    [Fact]
    public void Build_WhenCloudNameMissing_UsesFallback()
    {
        var url = Create(cloudName: "").Build("products/abc", ImageTransformation.Card);

        url.ShouldContain(ProductImageUrls.FallbackCloudName);
        url.ShouldNotContain("//image");
    }

    [Fact]
    public void Resolve_WhenNotMigrated_ReturnsSourceUrl()
    {
        var urls = Create();
        var image = new ProductImage { SourceUrl = "https://i.dr.com.tr/kapak.jpg", IsMigrated = false };

        urls.Resolve(image, ImageTransformation.Card).ShouldBe("https://i.dr.com.tr/kapak.jpg");
    }

    [Fact]
    public void Resolve_WhenMigratedButPublicIdNull_ReturnsSourceUrl()
    {
        var urls = Create();
        var image = new ProductImage
        {
            SourceUrl = "https://i.dr.com.tr/kapak.jpg",
            IsMigrated = true,
            CloudinaryPublicId = null
        };

        urls.Resolve(image, ImageTransformation.Card).ShouldBe("https://i.dr.com.tr/kapak.jpg");
    }

    [Fact]
    public void Resolve_WhenMigrated_ReturnsCloudinaryUrl()
    {
        var urls = Create();
        var image = new ProductImage
        {
            SourceUrl = "https://i.dr.com.tr/kapak.jpg",
            IsMigrated = true,
            CloudinaryPublicId = "products/abc"
        };

        urls.Resolve(image, ImageTransformation.Card).ShouldBe(urls.CardPrefix + "products/abc");
    }
}
