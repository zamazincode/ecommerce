using Commerce.Api.Persistence;
using Commerce.Api.Persistence.Seeding.Import;
using Commerce.Domain.Catalog;
using Commerce.Domain.Common;
using Commerce.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Commerce.IntegrationTests.Catalog;

/// Aktarımın uçtan uca testi. Importer dosya değil ham satır listesi aldığı
/// için burada Excel dosyası üretmeye gerek yok — kaynaktaki tuzakları
/// (kitap dışı ürün, çok yazarlı kitap, başlıksız satır, aynı yazarın iki
/// yazımı) elle kurgulayıp gerçek Postgres'e yazıyoruz.
public class CatalogImporterTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTime Now = new(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);

    private static ImportRawRow Row(int line, Dictionary<string, string?> overrides)
    {
        var cells = new Dictionary<string, string?>
        {
            [ImportColumns.Sku] = "0000000000001",
            [ImportColumns.Barcode] = "9780000000001",
            [ImportColumns.Title] = "Test Kitabı",
            [ImportColumns.Brand] = "None",
            [ImportColumns.Publisher] = "Can Yayınları",
            [ImportColumns.Author] = "Sabahattin Ali",
            [ImportColumns.Contributors] = "None",
            [ImportColumns.Category1] = "Kitap",
            [ImportColumns.Category2] = "Edebiyat",
            [ImportColumns.Category3] = "Roman",
            [ImportColumns.Category4] = "Türk Romanı",
            [ImportColumns.ListPrice] = "200",
            [ImportColumns.SalePrice] = "150",
            [ImportColumns.PageCount] = "160",
            [ImportColumns.PublishedYear] = "2020",
            [ImportColumns.Language] = "Turkish",
            [ImportColumns.Binding] = "Paperback",
            [ImportColumns.MainImage] = "https://i.dr.com.tr/kapak-1.jpg",
            [ImportColumns.AllImages] = "https://i.dr.com.tr/kapak-1.jpg",
            [ImportColumns.DescriptionText] = "Gerçek Türkçe bir tanıtım metni."
        };

        foreach (var (key, value) in overrides) cells[key] = value;

        return new ImportRawRow(cells) { SourceLineNumber = line };
    }

    /// Kaynağın küçük ama temsili bir kesiti.
    private static List<ImportRawRow> SampleRows() =>
    [
        // 1) Sıradan kitap
        Row(2, []),

        // 2) Çok yazarlı kitap + çevirmen (çevirmen Author OLMAMALI)
        Row(3, new Dictionary<string, string?>
        {
            [ImportColumns.Sku] = "0000000000002",
            [ImportColumns.Barcode] = "9780000000002",
            [ImportColumns.Title] = "Kimya Bize Ne Anlatıyor",
            [ImportColumns.Contributors] = """
                [
                  {"label":"Yazar","kind":"yazar","name":"Lisa Jane Gillespie"},
                  {"label":"Yazar","kind":"yazar","name":"Alex Frith"},
                  {"label":"Çevirmen","kind":"yazar","name":"Ekrem Demirli"}
                ]
                """,
            [ImportColumns.Category2] = "Bilim",
            [ImportColumns.Category3] = "Popüler Bilim",
            [ImportColumns.Category4] = "None",
            [ImportColumns.ListPrice] = "72.9",
            [ImportColumns.SalePrice] = "72.9",
            [ImportColumns.AllImages] =
                "https://i.dr.com.tr/kimya-1.jpg | https://i.dr.com.tr/kimya-2.jpg"
        }),

        // 3) Kitap dışı ürün: markalı, BookDetail'siz
        Row(4, new Dictionary<string, string?>
        {
            [ImportColumns.Sku] = "0000000000003",
            [ImportColumns.Title] = "JBL Tune 510BT Kulaklık",
            [ImportColumns.Brand] = "JBL",
            [ImportColumns.Publisher] = "None",
            [ImportColumns.Author] = "None",
            [ImportColumns.Category1] = "Elektronik",
            [ImportColumns.Category2] = "Ev Elektroniği",
            [ImportColumns.Category3] = "Kulaklıklar",
            [ImportColumns.Category4] = "None",
            [ImportColumns.Language] = "None",
            [ImportColumns.Binding] = "None",
            [ImportColumns.PageCount] = "None",
            [ImportColumns.PublishedYear] = "None"
        }),

        // 4) Başlığı boş satır → atlanmalı
        Row(5, new Dictionary<string, string?>
        {
            [ImportColumns.Sku] = "0000000000004",
            [ImportColumns.Title] = "None"
        })
    ];

    private static Task<ImportReport> ImportAsync(AppDbContext db, IReadOnlyList<ImportRawRow> rows)
        => new CatalogImporter(db).ImportAsync(
            rows, new ImportOptions { PurgeCatalog = true, NowUtc = Now }, Ct);

    [Fact]
    public async Task Import_WritesProductsCategoriesAuthorsAndImages()
    {
        var report = await ExecuteDbAsync(db => ImportAsync(db, SampleRows()));

        report.RowsRead.ShouldBe(4);
        report.ProductsCreated.ShouldBe(3);
        report.Skipped.Count.ShouldBe(1);
        report.Skipped[0].Reason.ShouldBe("Başlık boş");

        await ExecuteDbAsync(async db =>
        {
            (await db.Products.CountAsync(Ct)).ShouldBe(3);

            // Kitap > Edebiyat > Roman > Türk Romanı (4)
            // Kitap > Bilim > Popüler Bilim (2 yeni)
            // Elektronik > Ev Elektroniği > Kulaklıklar (3 yeni)
            (await db.Categories.CountAsync(Ct)).ShouldBe(9);

            // Sabahattin Ali + Lisa Jane Gillespie + Alex Frith.
            // Çevirmen Ekrem Demirli LİSTEDE OLMAMALI.
            var authors = await db.Authors.Select(a => a.Name).ToListAsync(Ct);
            authors.Count.ShouldBe(3);
            authors.ShouldNotContain("Ekrem Demirli");

            (await db.Publishers.CountAsync(Ct)).ShouldBe(1);
            (await db.Brands.CountAsync(Ct)).ShouldBe(1);
            (await db.ProductImages.CountAsync(Ct)).ShouldBe(4);   // 1 + 2 + 1
        });
    }

    [Fact]
    public async Task Import_BuildsCategoryTreeWithCorrectParents()
    {
        await ExecuteDbAsync(db => ImportAsync(db, SampleRows()));

        await ExecuteDbAsync(async db =>
        {
            var leaf = await db.Categories
                .Include(c => c.Parent!).ThenInclude(c => c.Parent!).ThenInclude(c => c.Parent)
                .SingleAsync(c => c.Name == "Türk Romanı", Ct);

            leaf.Parent!.Name.ShouldBe("Roman");
            leaf.Parent.Parent!.Name.ShouldBe("Edebiyat");
            leaf.Parent.Parent.Parent!.Name.ShouldBe("Kitap");
            leaf.Parent.Parent.Parent.ParentId.ShouldBeNull();
        });
    }

    [Fact]
    public async Task Import_MapsPricesAndDiscountCorrectly()
    {
        await ExecuteDbAsync(db => ImportAsync(db, SampleRows()));

        await ExecuteDbAsync(async db =>
        {
            var discounted = await db.Products.SingleAsync(p => p.Sku == "0000000000001", Ct);
            discounted.Price.ShouldBe(200m);
            discounted.DiscountedPrice.ShouldBe(150m);

            // Liste = satış olan üründe indirim YAZILMAMALI.
            var plain = await db.Products.SingleAsync(p => p.Sku == "0000000000002", Ct);
            plain.Price.ShouldBe(72.9m);
            plain.DiscountedPrice.ShouldBeNull();
        });
    }

    [Fact]
    public async Task Import_WritesBookDetailOnlyForBooks()
    {
        await ExecuteDbAsync(db => ImportAsync(db, SampleRows()));

        await ExecuteDbAsync(async db =>
        {
            var book = await db.Products
                .Include(p => p.BookDetail)
                .SingleAsync(p => p.Sku == "0000000000001", Ct);

            book.BookDetail.ShouldNotBeNull();
            book.BookDetail.Isbn.ShouldBe("9780000000001");
            book.BookDetail.Language.ShouldBe("Türkçe");
            book.BookDetail.Binding.ShouldBe(BookBinding.Paperback);
            book.PublisherName.ShouldBe("Can Yayınları");
            book.BrandName.ShouldBeNull();

            var headphones = await db.Products
                .Include(p => p.BookDetail)
                .SingleAsync(p => p.Sku == "0000000000003", Ct);

            headphones.BookDetail.ShouldBeNull();
            headphones.BrandName.ShouldBe("JBL");
            headphones.PublisherId.ShouldBeNull();
        });
    }

    [Fact]
    public async Task Import_RunningTwice_DoesNotDuplicateAndKeepsSlugs()
    {
        await ExecuteDbAsync(db => ImportAsync(db, SampleRows()));

        var slugsBefore = await ExecuteDbAsync(db => db.Products
            .OrderBy(p => p.Sku)
            .Select(p => new { p.Sku, p.Slug })
            .ToListAsync(Ct));

        // İkinci tur: katalog temizlenmeden, SKU üzerinden upsert.
        var second = await ExecuteDbAsync(db => new CatalogImporter(db).ImportAsync(
            SampleRows(), new ImportOptions { PurgeCatalog = false, NowUtc = Now }, Ct));

        second.ProductsCreated.ShouldBe(0);
        second.ProductsUpdated.ShouldBe(3);

        await ExecuteDbAsync(async db =>
        {
            (await db.Products.CountAsync(Ct)).ShouldBe(3);
            (await db.Categories.CountAsync(Ct)).ShouldBe(9);
            (await db.Authors.CountAsync(Ct)).ShouldBe(3);
            // Yazar bağlantıları Clear/Add ile yenileniyor; kopyalanmamalı.
            (await db.ProductAuthors.CountAsync(Ct)).ShouldBe(3);
            (await db.ProductImages.CountAsync(Ct)).ShouldBe(4);

            var slugsAfter = await db.Products
                .OrderBy(p => p.Sku)
                .Select(p => new { p.Sku, p.Slug })
                .ToListAsync(Ct);

            // Yayınlanmış URL'ler kırılmamalı.
            slugsAfter.ShouldBe(slugsBefore);
        });
    }

    [Fact]
    public async Task Import_ResolvesDuplicateTitlesAndUnsluggableNames()
    {
        List<ImportRawRow> rows =
        [
            Row(2, new Dictionary<string, string?>
            {
                [ImportColumns.Sku] = "9786256370975",
                [ImportColumns.Title] = "Reyhan"
            }),
            Row(3, new Dictionary<string, string?>
            {
                [ImportColumns.Sku] = "9789752110113",
                [ImportColumns.Title] = "Reyhan"
            }),
            // Kaynakta 3 başlık tamamen Kiril — SlugGenerator boş üretir.
            Row(4, new Dictionary<string, string?>
            {
                [ImportColumns.Sku] = "0002145723001",
                [ImportColumns.Title] = "Земляничная фея"
            })
        ];

        await ExecuteDbAsync(db => ImportAsync(db, rows));

        await ExecuteDbAsync(async db =>
        {
            var slugs = await db.Products.OrderBy(p => p.Sku).Select(p => p.Slug).ToListAsync(Ct);

            slugs.Count.ShouldBe(3);
            slugs.Distinct().Count().ShouldBe(3);
            slugs.ShouldContain("urun-0002145723001");
            slugs.ShouldAllBe(s => s.Length > 0);
        });
    }

    [Fact]
    public async Task Import_MergesLookupEntriesThatShareASlug()
    {
        // Kaynakta "H. G. Wells" ile "H.G. Wells" aynı kişi, "Ast" ile "ast"
        // aynı yayınevi. Ada göre ayırsaydık slug unique index'i patlardı.
        List<ImportRawRow> rows =
        [
            Row(2, new Dictionary<string, string?>
            {
                [ImportColumns.Sku] = "0000000000010",
                [ImportColumns.Title] = "Zaman Makinesi",
                [ImportColumns.Author] = "H. G. Wells",
                [ImportColumns.Publisher] = "Ast"
            }),
            Row(3, new Dictionary<string, string?>
            {
                [ImportColumns.Sku] = "0000000000011",
                [ImportColumns.Title] = "Görünmez Adam",
                [ImportColumns.Author] = "H.G. Wells",
                [ImportColumns.Publisher] = "ast"
            })
        ];

        var report = await ExecuteDbAsync(db => ImportAsync(db, rows));

        report.AuthorsCreated.ShouldBe(1);
        report.PublishersCreated.ShouldBe(1);

        await ExecuteDbAsync(async db =>
        {
            (await db.Authors.CountAsync(Ct)).ShouldBe(1);
            (await db.Publishers.CountAsync(Ct)).ShouldBe(1);
            (await db.ProductAuthors.CountAsync(Ct)).ShouldBe(2);
        });
    }

    [Fact]
    public async Task Import_WithPurge_RemovesExistingCatalog()
    {
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            db.Products.Add(new ProductBuilder().WithName("Sahte Ürün").InCategory(category).Build());
            await db.SaveChangesAsync(Ct);
        });

        await ExecuteDbAsync(db => ImportAsync(db, SampleRows()));

        await ExecuteDbAsync(async db =>
        {
            (await db.Products.CountAsync(Ct)).ShouldBe(3);
            (await db.Products.AnyAsync(p => p.Name == "Sahte Ürün", Ct)).ShouldBeFalse();
            // Sahte kategori de gitmeli.
            (await db.Categories.AnyAsync(c => c.Name == "Roman" && c.ParentId == null, Ct))
                .ShouldBeFalse();
        });
    }

    [Fact]
    public async Task Import_GeneratesDeterministicStockAndCreatedAt()
    {
        await ExecuteDbAsync(db => ImportAsync(db, SampleRows()));

        var first = await ExecuteDbAsync(db => db.Products
            .OrderBy(p => p.Sku)
            .Select(p => new { p.Sku, p.Stock, p.CreatedAt })
            .ToListAsync(Ct));

        // Aynı kaynağı sıfırdan yükle: stok ve tarih aynı çıkmalı.
        await ExecuteDbAsync(db => ImportAsync(db, SampleRows()));

        var second = await ExecuteDbAsync(db => db.Products
            .OrderBy(p => p.Sku)
            .Select(p => new { p.Sku, p.Stock, p.CreatedAt })
            .ToListAsync(Ct));

        second.ShouldBe(first);
        first.ShouldAllBe(p => p.CreatedAt <= Now);
    }

    [Fact]
    public async Task Import_SkipsDuplicateSkuInSameFile()
    {
        List<ImportRawRow> rows =
        [
            Row(2, []),
            Row(3, new Dictionary<string, string?> { [ImportColumns.Title] = "Aynı SKU" })
        ];

        var report = await ExecuteDbAsync(db => ImportAsync(db, rows));

        report.ProductsCreated.ShouldBe(1);
        report.Skipped.Count.ShouldBe(1);
        report.Skipped[0].Reason.ShouldBe("SKU dosyada tekrar ediyor");
    }

    [Fact]
    public async Task Import_KeepsProductsWithoutSkuWhenNotPurging()
    {
        // Elle eklenmiş (SKU'suz) ürün --keep modunda silinmemeli.
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            db.Products.Add(new ProductBuilder().WithName("Elle Eklenen").InCategory(category).Build());
            await db.SaveChangesAsync(Ct);
        });

        await ExecuteDbAsync(db => new CatalogImporter(db).ImportAsync(
            SampleRows(), new ImportOptions { PurgeCatalog = false, NowUtc = Now }, Ct));

        await ExecuteDbAsync(async db =>
        {
            (await db.Products.CountAsync(Ct)).ShouldBe(4);
            (await db.Products.AnyAsync(p => p.Name == "Elle Eklenen", Ct)).ShouldBeTrue();
        });
    }
}
