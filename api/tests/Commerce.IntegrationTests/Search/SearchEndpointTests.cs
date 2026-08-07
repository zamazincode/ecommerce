using System.Net;
using System.Net.Http.Json;
using Commerce.Api.Features.Search;
using Commerce.Api.Persistence;
using Commerce.Domain.Catalog;
using Commerce.Domain.Common;
using Commerce.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Commerce.IntegrationTests.Search;

public class SearchEndpointTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<SearchResultDto> SearchAsync(string url)
    {
        var response = await Client.GetAsync(url, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SearchResultDto>(Ct);
        result.ShouldNotBeNull();
        return result;
    }

    private async Task<List<SuggestionDto>> SuggestAsync(string url)
    {
        var response = await Client.GetAsync(url, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<SuggestionDto>>(Ct);
        result.ShouldNotBeNull();
        return result;
    }

    /// Ortak katalog: "Suç ve Ceza" (başlıkta "Ceza" geçiyor, yazarı
    /// Dostoyevski) + "Bambaşka Bir Kitap" (sadece açıklamada "ceza" geçiyor).
    /// setweight A>D'yi ve DidYouMean'in yazar kaynağını doğrulamak için
    /// tasarlandı (bkz. plan Ölçüm 2.8/2.9).
    private async Task<(Category Category, Author Dostoyevski)> SeedSearchableCatalogAsync()
        => await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);

            var dostoyevski = new Author
            {
                Name = "Fyodor Dostoyevski",
                Slug = SlugGenerator.Generate("Fyodor Dostoyevski")
            };
            db.Authors.Add(dostoyevski);

            db.Products.AddRange(
                new ProductBuilder()
                    .WithName("Suç ve Ceza")
                    .InCategory(category)
                    .ByAuthor(dostoyevski)
                    .Build(),
                new ProductBuilder()
                    .WithName("Bambaşka Bir Kitap")
                    .WithDescription("Bu kitapta suç ve ceza kavramları tartışılır.")
                    .InCategory(category)
                    .Build());

            await db.SaveChangesAsync();
            return (category, dostoyevski);
        });

    [Fact]
    public async Task Search_WithExactWord_ReturnsMatchingProducts()
    {
        await SeedSearchableCatalogAsync();

        var result = await SearchAsync("/api/search?q=ceza");

        result.Results.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Search_RanksTitleMatchAboveDescriptionMatch()
    {
        await SeedSearchableCatalogAsync();

        var result = await SearchAsync("/api/search?q=ceza");

        result.Results.Items[0].Name.ShouldBe("Suç ve Ceza");
    }

    [Fact]
    public async Task Search_IsAccentInsensitive()
    {
        await SeedSearchableCatalogAsync();

        var accented = await SearchAsync($"/api/search?q={Uri.EscapeDataString("suç")}");
        var plain = await SearchAsync("/api/search?q=suc");

        accented.Results.TotalCount.ShouldBeGreaterThan(0);
        accented.Results.TotalCount.ShouldBe(plain.Results.TotalCount);
    }

    [Fact]
    public async Task Search_MatchesAuthorName()
    {
        await SeedSearchableCatalogAsync();

        var result = await SearchAsync("/api/search?q=dostoyevski");

        result.Results.TotalCount.ShouldBe(1);
        result.Results.Items[0].Name.ShouldBe("Suç ve Ceza");
    }

    [Fact]
    public async Task Search_MatchesPublisherName()
    {
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            db.Products.Add(new ProductBuilder()
                .WithName("Kumarbaz")
                .InCategory(category)
                .WithPublisherName("İletişim Yayınları")
                .Build());
            await db.SaveChangesAsync();
        });

        var result = await SearchAsync("/api/search?q=iletisim");

        result.Results.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task Search_WithTypo_ReturnsDidYouMeanSuggestion()
    {
        await SeedSearchableCatalogAsync();

        var result = await SearchAsync("/api/search?q=dostoyevsky");

        result.Results.TotalCount.ShouldBe(0);
        result.DidYouMean.ShouldBe("Fyodor Dostoyevski");
    }

    [Fact]
    public async Task Search_WithNonsenseTerm_ReturnsNullDidYouMean()
    {
        await SeedSearchableCatalogAsync();

        var result = await SearchAsync("/api/search?q=zzzqqqxx");

        result.Results.TotalCount.ShouldBe(0);
        result.DidYouMean.ShouldBeNull();
    }

    [Fact]
    public async Task Search_CombinedWithCategoryFilter_NarrowsResults()
    {
        var polisiyeId = 0;
        await ExecuteDbAsync(async db =>
        {
            var roman = CatalogTestData.DefaultCategory("Roman");
            var polisiye = CatalogTestData.DefaultCategory("Polisiye");
            db.Categories.AddRange(roman, polisiye);

            db.Products.AddRange(
                new ProductBuilder().WithName("Kayıp Kitap").InCategory(roman).Build(),
                new ProductBuilder().WithName("Kayıp Dosya").InCategory(polisiye).Build());

            await db.SaveChangesAsync();
            polisiyeId = polisiye.Id;
        });

        var all = await SearchAsync("/api/search?q=kayip");
        var filtered = await SearchAsync($"/api/search?q=kayip&categoryId={polisiyeId}");

        all.Results.TotalCount.ShouldBe(2);
        filtered.Results.TotalCount.ShouldBe(1);
        filtered.Results.Items[0].Name.ShouldBe("Kayıp Dosya");
    }

    [Fact]
    public async Task Search_WithPriceFilter_NarrowsResults()
    {
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);

            db.Products.AddRange(
                new ProductBuilder().WithName("Ucuz Roman").WithPrice(30m).InCategory(category).Build(),
                new ProductBuilder().WithName("Pahalı Roman").WithPrice(500m).InCategory(category).Build(),
                // 400₺ ama indirimli 80₺ → maxPrice=100 filtresine GİRMELİ
                new ProductBuilder().WithName("İndirimli Roman").WithPrice(400m).WithDiscount(80m).InCategory(category).Build());

            await db.SaveChangesAsync();
        });

        var result = await SearchAsync("/api/search?q=roman&maxPrice=100");

        result.Results.TotalCount.ShouldBe(2);
        result.Results.Items.ShouldAllBe(p => p.EffectivePrice <= 100m);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("%20")]
    public async Task Search_WithEmptyOrTooShortTerm_Returns400(string q)
    {
        var response = await Client.GetAsync($"/api/search?q={q}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_WithTsQuerySyntaxCharacters_DoesNotThrow()
    {
        await SeedSearchableCatalogAsync();

        var response = await Client.GetAsync(
            "/api/search?q=" + Uri.EscapeDataString("ceza & | !:*"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Search_ExcludesInactiveProducts()
    {
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            db.Products.AddRange(
                new ProductBuilder().WithName("Aktif Roman").InCategory(category).Build(),
                new ProductBuilder().WithName("Pasif Roman").Inactive().InCategory(category).Build());
            await db.SaveChangesAsync();
        });

        var result = await SearchAsync("/api/search?q=roman");

        result.Results.TotalCount.ShouldBe(1);
        result.Results.Items[0].Name.ShouldBe("Aktif Roman");
    }

    [Fact]
    public async Task Search_WritesSearchLog()
    {
        await SeedSearchableCatalogAsync();

        await SearchAsync("/api/search?q=ceza");

        var log = await ExecuteDbAsync(db => db.SearchLogs
            .OrderByDescending(l => l.Id)
            .FirstAsync(Ct));

        log.Term.ShouldBe("ceza");
        log.ResultCount.ShouldBe(2);
    }

    [Fact]
    public async Task Search_IsPaged()
    {
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            for (var i = 0; i < 5; i++)
                db.Products.Add(new ProductBuilder().WithName($"Roman {i}").InCategory(category).Build());
            await db.SaveChangesAsync();
        });

        var result = await SearchAsync("/api/search?q=roman&pageSize=2");

        result.Results.TotalCount.ShouldBe(5);
        result.Results.Items.Count.ShouldBe(2);
        result.Results.TotalPages.ShouldBe(3);
        result.Results.HasNext.ShouldBeTrue();
    }

    [Fact]
    public async Task Suggest_WithPartialTerm_ReturnsSuggestions()
    {
        await SeedSearchableCatalogAsync();

        var result = await SuggestAsync("/api/search/suggest?q=su");

        result.ShouldContain(s => s.Name == "Suç ve Ceza");
    }

    [Fact]
    public async Task Suggest_MatchesAuthorFragment()
    {
        await SeedSearchableCatalogAsync();

        var result = await SuggestAsync("/api/search/suggest?q=dosto");

        result.ShouldContain(s => s.Name == "Suç ve Ceza");
    }

    [Fact]
    public async Task Suggest_WithTooShortTerm_ReturnsEmptyList()
    {
        var result = await SuggestAsync("/api/search/suggest?q=s");

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task Suggest_ReturnsAtMostEightItems()
    {
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            for (var i = 0; i < 12; i++)
                db.Products.Add(new ProductBuilder().WithName($"Roman {i:D2}").InCategory(category).Build());
            await db.SaveChangesAsync();
        });

        var result = await SuggestAsync("/api/search/suggest?q=roman");

        result.Count.ShouldBe(8);
    }
}
