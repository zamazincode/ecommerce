using Commerce.Domain.Common;
using Shouldly;

namespace Commerce.UnitTests.Common;

public class SlugGeneratorTests
{
    [Theory]
    [InlineData("Suç ve Ceza", "suc-ve-ceza")]
    [InlineData("Kürk Mantolu Madonna", "kurk-mantolu-madonna")]
    [InlineData("İnce Memed", "ince-memed")]
    [InlineData("Işıklar Sönerken", "isiklar-sonerken")]
    [InlineData("Çağdaş Türk Şiiri", "cagdas-turk-siiri")]
    [InlineData("1984", "1984")]
    [InlineData("  Boşluklu   Metin  ", "bosluklu-metin")]
    [InlineData("Özel!@#Karakterler", "ozel-karakterler")]
    public void Generate_WithTurkishInput_ProducesAsciiSlug(string input, string expected)
    {
        var result = SlugGenerator.Generate(input);

        result.ShouldBe(expected);
    }

    [Fact]
    public void Generate_WhenInputIsEmpty_ReturnsEmptyString()
    {
        SlugGenerator.Generate("").ShouldBe("");
        SlugGenerator.Generate("   ").ShouldBe("");
    }
}