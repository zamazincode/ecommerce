using Commerce.Domain.Common;
using Shouldly;

namespace Commerce.UnitTests.Search;

public class SearchTermNormalizerTests
{
    [Theory]
    [InlineData("Suç", "suc")]
    [InlineData("SUÇ", "suc")]
    [InlineData("suc", "suc")]
    [InlineData("Işık", "isik")]
    [InlineData("İSTANBUL", "istanbul")]
    [InlineData("Çağdaş", "cagdas")]
    [InlineData("  boşluk   temizliği  ", "bosluk temizligi")]
    public void Normalize_HandlesTurkishCharactersAndCasing(string input, string expected)
    {
        SearchTermNormalizer.Normalize(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("kitap & roman", "kitap roman")]
    [InlineData("test | ' ; --", "test")]
    [InlineData("a!b@c#", "a b c")]
    public void Normalize_StripsSpecialCharacters(string input, string expected)
    {
        SearchTermNormalizer.Normalize(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    [InlineData("!")]
    public void IsValid_RejectsEmptyOrTooShortTerms(string? input)
    {
        SearchTermNormalizer.IsValid(input).ShouldBeFalse();
    }

    [Fact]
    public void Normalize_TruncatesVeryLongTerms()
    {
        var input = new string('a', 500);

        var result = SearchTermNormalizer.Normalize(input);

        result.Length.ShouldBe(SearchTermNormalizer.MaxLength);
    }

    // .NET'te "İ".ToLowerInvariant() kültüre göre iki karakterli (i + U+0307)
    // sonuç verebiliyor; veritabanı tarafı lower()+unaccent() ile tek karakterli
    // "i" üretiyor. Eşleme tablosu bu farkı ortadan kaldırmalı.
    [Fact]
    public void Normalize_MapsDottedAndDotlessI()
    {
        SearchTermNormalizer.Normalize("İIı").ShouldBe("iii");
    }
}
