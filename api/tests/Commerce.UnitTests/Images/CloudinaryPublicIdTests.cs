using Commerce.Api.Common.Images;
using Shouldly;

namespace Commerce.UnitTests.Images;

public class CloudinaryPublicIdTests
{
    [Theory]
    [InlineData("products/abc123XYZ_-")]
    [InlineData("abc123")] // klasörsüz — dynamic folder modu
    public void IsValid_WithWellFormedPublicId_ReturnsTrue(string publicId)
        => CloudinaryPublicId.IsValid(publicId).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("products/../../etc/passwd")]
    [InlineData("products/a b")]
    [InlineData("products/a?x=1")]
    [InlineData("products/a/b")]
    public void IsValid_WithMalformedPublicId_ReturnsFalse(string? publicId)
        => CloudinaryPublicId.IsValid(publicId).ShouldBeFalse();

    [Fact]
    public void IsValid_WhenLongerThan200Characters_ReturnsFalse()
    {
        var tooLong = "products/" + new string('a', 200);

        CloudinaryPublicId.IsValid(tooLong).ShouldBeFalse();
    }
}
