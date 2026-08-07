using Commerce.Api.Persistence.Seeding.Import;
using Shouldly;

namespace Commerce.UnitTests.Import;

public class SlugRegistryTests
{
    [Fact]
    public void Allocate_ReturnsPlainSlugWhenFree()
    {
        var registry = new SlugRegistry();

        registry.Allocate("Suç ve Ceza", "0001").ShouldBe("suc-ve-ceza");
    }

    [Fact]
    public void Allocate_OnCollision_AppendsSkuSuffix()
    {
        // Kaynakta "Reyhan", "Sessizlik" ve "Aya Yolculuk" ikişer kez geçiyor —
        // farklı kitaplar, aynı ad.
        var registry = new SlugRegistry();

        var first = registry.Allocate("Reyhan", "9786256370975");
        var second = registry.Allocate("Reyhan", "9789752110113");

        first.ShouldBe("reyhan");
        second.ShouldBe("reyhan-110113");   // SKU'nun son 6 hanesi
    }

    [Fact]
    public void Allocate_IsIndependentOfRowOrder()
    {
        // Satır sırası değişse bile ikinci ürün aynı slug'ı almalı; aksi
        // halde her aktarımda URL'ler değişir.
        var forward = new SlugRegistry();
        forward.Allocate("Reyhan", "9786256370975");
        var secondForward = forward.Allocate("Reyhan", "9789752110113");

        var reverse = new SlugRegistry();
        reverse.Allocate("Reyhan", "9789752110113");
        var firstReverse = reverse.Allocate("Reyhan", "9786256370975");

        secondForward.ShouldBe("reyhan-110113");
        firstReverse.ShouldBe("reyhan-370975");
    }

    [Fact]
    public void Allocate_WhenNameProducesNoSlug_UsesFallbackKey()
    {
        // Kaynakta 3 başlık tamamen Kiril: SlugGenerator boş string döndürür.
        var registry = new SlugRegistry();

        registry.Allocate("Земляничная фея", "0002145723001")
            .ShouldBe("urun-0002145723001");
    }

    [Fact]
    public void Allocate_RespectsReservedSlugs()
    {
        // Katalog temizlenmeden çalıştırıldığında mevcut ürünlerin slug'ları
        // rezerve edilir; unique index ihlali olmamalı.
        var registry = new SlugRegistry();
        registry.Reserve("suc-ve-ceza");

        registry.Allocate("Suç ve Ceza", "0001").ShouldNotBe("suc-ve-ceza");
    }

    [Fact]
    public void Allocate_WhenSuffixAlsoTaken_FallsBackToCounter()
    {
        var registry = new SlugRegistry();
        registry.Reserve("reyhan");
        registry.Reserve("reyhan-110113");

        registry.Allocate("Reyhan", "9789752110113").ShouldBe("reyhan-2");
    }

    [Fact]
    public void Allocate_UsesCustomEmptyFallback()
    {
        var registry = new SlugRegistry();

        registry.Allocate("Ελλάδα", emptyFallback: "kategori").ShouldBe("kategori");
    }
}
