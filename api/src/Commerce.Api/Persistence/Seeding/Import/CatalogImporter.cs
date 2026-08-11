using System.Diagnostics.CodeAnalysis;
using Commerce.Domain.Catalog;
using Commerce.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Commerce.Api.Persistence.Seeding.Import;

public sealed record ImportOptions
{
    /// Varsayılan: katalog tabloları boşaltılır ve yalnızca kaynaktaki veri kalır.
    /// false verilirse mevcut ürünler korunur, SKU üzerinden upsert yapılır.
    public bool PurgeCatalog { get; init; } = true;

    /// Üretilen CreatedAt/UpdatedAt damgalarının referans anı.
    public DateTime NowUtc { get; init; } = DateTime.UtcNow;
}

/// Ham satırları katalog tablolarına yazar.
///
/// Dosya OKUMAZ — <see cref="ImportRawRow"/> listesi alır. Excel'i
/// <see cref="ExcelWorkbookReader"/> okur; bu ayrım sayesinde aktarımın
/// tamamı testte dosyasız çalıştırılabiliyor.
///
/// Aynı kaynakla iki kez çalıştırmak güvenlidir: eşleştirme
/// <see cref="Product.Sku"/> üzerinden yapılır, eşleşen ürünün slug'ı
/// KORUNUR (yayınlanmış URL'ler kırılmasın).
public sealed class CatalogImporter(AppDbContext db, ILogger<CatalogImporter>? logger = null)
{
    // İsteğe bağlı: CatalogImportCommand ve testler DI kullanmadan `new CatalogImporter(db)`
    // çağırıyor. Logger verilmezse sessiz bir yedeğe düşer.
    private readonly ILogger<CatalogImporter> _logger = logger ?? NullLogger<CatalogImporter>.Instance;


    /// Kategori adlarında geçemeyecek bir ayraç (ASCII unit separator).
    private const char PathSeparator = '\u001F';

    /// Change tracker şişmesin diye her bu kadar üründe bir kaydediyoruz.
    private const int SaveBatchSize = 500;

    public async Task<ImportReport> ImportAsync(
        IReadOnlyList<ImportRawRow> rawRows,
        ImportOptions options,
        CancellationToken ct = default)
    {
        var report = new ImportReport { RowsRead = rawRows.Count };

        var rows = ParseRows(rawRows, report);

        if (options.PurgeCatalog)
            await PurgeCatalogAsync(ct);

        var categories = await EnsureCategoriesAsync(rows, report, ct);
        var publishers = await EnsurePublishersAsync(rows, report, ct);
        var brands = await EnsureBrandsAsync(rows, report, ct);
        var authors = await EnsureAuthorsAsync(rows, report, ct);

        await UpsertProductsAsync(rows, categories, publishers, brands, authors, options, report, ct);

        return report;
    }

    // ─────────────────────────────────────────────────────────────
    // 1. Ayrıştırma
    // ─────────────────────────────────────────────────────────────
    private static List<ProductImportRow> ParseRows(
        IReadOnlyList<ImportRawRow> rawRows, ImportReport report)
    {
        var rows = new List<ProductImportRow>(rawRows.Count);
        var seenSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in rawRows)
        {
            if (!ProductRowMapper.TryMap(raw, out var row, out var reason))
            {
                report.Skipped.Add((
                    raw.SourceLineNumber,
                    ExcelRowParser.Text(raw[ImportColumns.Sku]) ?? "-",
                    reason!));
                continue;
            }

            // Aynı SKU iki satırda gelirse ikincisi birincisini ezmesin —
            // unique index zaten patlardı, sebebi burada görünsün.
            if (!seenSkus.Add(row.Sku))
            {
                report.Skipped.Add((raw.SourceLineNumber, row.Sku, "SKU dosyada tekrar ediyor"));
                continue;
            }

            rows.Add(row);
        }

        return rows;
    }

    // ─────────────────────────────────────────────────────────────
    // 2. Temizlik
    // ─────────────────────────────────────────────────────────────
    /// Sipariş satırlarına DOKUNULMAZ: OrderItem'da ProductId üzerinde FK yok
    /// (bilerek) ve geçmiş sipariş bilgisi zaten snapshot'ta duruyor.
    /// Kuponlar ve kullanıcılar da korunur.
    private async Task PurgeCatalogAsync(CancellationToken ct)
    {
        // Admin'in Cloudinary'ye yüklediği görseller de bu silmeyle gidiyor
        // (bilinçli — "temizle ve yeniden yükle" davranışı). Ama Cloudinary'deki
        // dosyalar YETİM KALIR ve satır gittiği için haftalık ImageCleanupJobs
        // onları bir daha asla bulamaz (plan 2.6) — en azından görünür kılıyoruz.
        var hosted = await db.ProductImages.IgnoreQueryFilters()
            .Where(i => i.IsMigrated && i.CloudinaryPublicId != null)
            .CountAsync(ct);
        if (hosted > 0)
            _logger.LogWarning(
                "{Count} adet Cloudinary görseli veritabanından siliniyor. Cloudinary'deki " +
                "dosyalar YETİM KALACAK ve haftalık temizlik job'ı onları bulamayacak.", hosted);

        // IgnoreQueryFilters olmadan soft-delete edilmiş ürünler geride kalır
        // ve ardından gelen Products silmesi FK'dan patlar.
        await db.Reviews.IgnoreQueryFilters().ExecuteDeleteAsync(ct);
        await db.Favorites.IgnoreQueryFilters().ExecuteDeleteAsync(ct);
        await db.CartItems.IgnoreQueryFilters().ExecuteDeleteAsync(ct);
        await db.ProductImages.IgnoreQueryFilters().ExecuteDeleteAsync(ct);
        await db.ProductAuthors.IgnoreQueryFilters().ExecuteDeleteAsync(ct);
        await db.BookDetails.IgnoreQueryFilters().ExecuteDeleteAsync(ct);
        await db.Products.IgnoreQueryFilters().ExecuteDeleteAsync(ct);

        await db.Authors.ExecuteDeleteAsync(ct);
        await db.Publishers.ExecuteDeleteAsync(ct);
        await db.Brands.ExecuteDeleteAsync(ct);

        // Kategori kendine referanslı ve FK davranışı Restrict: tek DELETE'te
        // üst kategori alt kategoriden önce silinirse hata alırız. Yapraktan
        // köke doğru katman katman siliyoruz (ağaç 4 seviye → 4 tur).
        while (true)
        {
            var deleted = await db.Categories
                .Where(c => !db.Categories.Any(child => child.ParentId == c.Id))
                .ExecuteDeleteAsync(ct);

            if (deleted == 0) break;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 3. Kategori ağacı
    // ─────────────────────────────────────────────────────────────
    private async Task<Dictionary<string, Category>> EnsureCategoriesAsync(
        IReadOnlyList<ProductImportRow> rows, ImportReport report, CancellationToken ct)
    {
        var existing = await db.Categories.ToListAsync(ct);
        var byId = existing.ToDictionary(c => c.Id);

        // Kategoriyi ADIYLA değil TAM YOLUYLA anahtarlıyoruz: "Matematik"
        // ağacın üç ayrı yerinde geçiyor, aynı düğüm değiller.
        var byPath = new Dictionary<string, Category>(StringComparer.Ordinal);
        foreach (var category in existing)
            byPath[PathKeyOf(category, byId)] = category;

        var slugs = new SlugRegistry();
        foreach (var category in existing) slugs.Reserve(category.Slug);

        // Kardeşler arasındaki sıra: eklenme sırası. Yolları önce sıraladığımız
        // için sonuç alfabetik ve her çalıştırmada aynı.
        var nextOrder = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var category in existing)
        {
            var parentKey = category.ParentId is { } parentId && byId.TryGetValue(parentId, out var parent)
                ? PathKeyOf(parent, byId)
                : string.Empty;

            nextOrder[parentKey] = Math.Max(
                nextOrder.GetValueOrDefault(parentKey), category.DisplayOrder + 1);
        }

        var paths = rows
            .Select(r => string.Join(PathSeparator, r.CategoryPath))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.InvariantCulture)
            .ToList();

        foreach (var path in paths)
        {
            Category? parent = null;
            var prefix = string.Empty;

            foreach (var name in path.Split(PathSeparator))
            {
                var key = prefix.Length == 0 ? name : prefix + PathSeparator + name;

                if (!byPath.TryGetValue(key, out var node))
                {
                    var order = nextOrder.GetValueOrDefault(prefix);
                    nextOrder[prefix] = order + 1;

                    node = new Category
                    {
                        Name = name,
                        Slug = AllocateCategorySlug(slugs, name, parent),
                        Parent = parent,
                        DisplayOrder = order
                    };

                    db.Categories.Add(node);
                    byPath[key] = node;
                    report.CategoriesCreated++;
                }

                parent = node;
                prefix = key;
            }
        }

        await db.SaveChangesAsync(ct);
        return byPath;
    }

    private static string PathKeyOf(Category category, Dictionary<int, Category> byId)
    {
        var names = new List<string> { category.Name };
        var current = category;

        while (current.ParentId is { } parentId && byId.TryGetValue(parentId, out var parent))
        {
            names.Add(parent.Name);
            current = parent;
        }

        names.Reverse();
        return string.Join(PathSeparator, names);
    }

    /// Kategori slug'ı önce sade adından üretilir ("roman"). Kaynakta 8 ad
    /// ağacın iki ayrı yerinde geçiyor ("Bilim", "Hobi", "Müzik", "Diğer"...);
    /// çakışanlarda üst kategori öneki ekleniyor ("kitap-bilim").
    private static string AllocateCategorySlug(SlugRegistry slugs, string name, Category? parent)
    {
        var plain = SlugGenerator.Generate(name);

        if (plain.Length > 0 && !slugs.IsTaken(plain))
        {
            slugs.Reserve(plain);
            return plain;
        }

        var prefixed = parent is null || plain.Length == 0 ? name : $"{parent.Slug}-{plain}";
        return slugs.Allocate(prefixed, emptyFallback: "kategori");
    }

    // ─────────────────────────────────────────────────────────────
    // 4. Yayınevi / marka / yazar
    // ─────────────────────────────────────────────────────────────
    private async Task<Dictionary<string, Publisher>> EnsurePublishersAsync(
        IReadOnlyList<ProductImportRow> rows, ImportReport report, CancellationToken ct)
    {
        var created = await EnsureLookupAsync(
            db.Publishers,
            p => p.Slug,
            rows.Select(r => r.PublisherName),
            (name, slug) => new Publisher { Name = name, Slug = slug },
            ct);

        report.PublishersCreated = created.Created;
        return created.BySlug;
    }

    private async Task<Dictionary<string, Brand>> EnsureBrandsAsync(
        IReadOnlyList<ProductImportRow> rows, ImportReport report, CancellationToken ct)
    {
        var created = await EnsureLookupAsync(
            db.Brands,
            b => b.Slug,
            rows.Select(r => r.BrandName),
            (name, slug) => new Brand { Name = name, Slug = slug },
            ct);

        report.BrandsCreated = created.Created;
        return created.BySlug;
    }

    private async Task<Dictionary<string, Author>> EnsureAuthorsAsync(
        IReadOnlyList<ProductImportRow> rows, ImportReport report, CancellationToken ct)
    {
        var created = await EnsureLookupAsync(
            db.Authors,
            a => a.Slug,
            rows.SelectMany(r => r.AuthorNames),
            (name, slug) => new Author { Name = name, Slug = slug },
            ct);

        report.AuthorsCreated = created.Created;
        return created.BySlug;
    }

    /// Yazar/yayınevi/marka için ortak "varsa bul, yoksa oluştur".
    ///
    /// Tekilleştirme ADA göre değil SLUG'a göre: kaynakta "H. G. Wells" ve
    /// "H.G. Wells" aynı kişi, "Ast" ile "ast" aynı yayınevi. Ada göre
    /// ayırsaydık slug unique index'i patlardı.
    private async Task<(Dictionary<string, TEntity> BySlug, int Created)> EnsureLookupAsync<TEntity>(
        DbSet<TEntity> set,
        Func<TEntity, string> slugOf,
        IEnumerable<string?> names,
        Func<string, string, TEntity> create,
        CancellationToken ct)
        where TEntity : class
    {
        var bySlug = (await set.ToListAsync(ct))
            .ToDictionary(slugOf, StringComparer.OrdinalIgnoreCase);

        var created = 0;

        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;

            var slug = SlugGenerator.Generate(name);
            if (slug.Length == 0 || bySlug.ContainsKey(slug)) continue;

            var entity = create(name, slug);
            set.Add(entity);
            bySlug[slug] = entity;
            created++;
        }

        await db.SaveChangesAsync(ct);
        return (bySlug, created);
    }

    // ─────────────────────────────────────────────────────────────
    // 5. Ürünler
    // ─────────────────────────────────────────────────────────────
    private async Task UpsertProductsAsync(
        IReadOnlyList<ProductImportRow> rows,
        Dictionary<string, Category> categories,
        Dictionary<string, Publisher> publishers,
        Dictionary<string, Brand> brands,
        Dictionary<string, Author> authors,
        ImportOptions options,
        ImportReport report,
        CancellationToken ct)
    {
        var existing = await db.Products
            .IgnoreQueryFilters()
            .Include(p => p.Images)
            .Include(p => p.ProductAuthors)
            .Include(p => p.BookDetail)
            .Where(p => p.Sku != null)
            .ToDictionaryAsync(p => p.Sku!, StringComparer.OrdinalIgnoreCase, ct);

        // Slug'ı olmayan (elle eklenmiş, SKU'suz) ürünlerin slug'ları da
        // rezerve edilmeli — yoksa unique index ihlali alırız.
        var slugs = new SlugRegistry();
        foreach (var slug in await db.Products.IgnoreQueryFilters().Select(p => p.Slug).ToListAsync(ct))
            slugs.Reserve(slug);

        var pending = 0;

        foreach (var row in rows)
        {
            if (!existing.TryGetValue(row.Sku, out var product))
            {
                product = new Product
                {
                    Sku = row.Sku,
                    // Slug SADECE ürün ilk kez yaratılırken belirlenir.
                    Slug = slugs.Allocate(row.Name, row.Sku),
                    CreatedAt = SyntheticValues.CreatedAtFor(row.Sku, options.NowUtc)
                };

                db.Products.Add(product);
                existing[row.Sku] = product;
                report.ProductsCreated++;
            }
            else
            {
                product.UpdatedAt = options.NowUtc;
                report.ProductsUpdated++;
            }

            ApplyRow(product, row, categories, publishers, brands, authors, report);

            if (++pending < SaveBatchSize) continue;

            await db.SaveChangesAsync(ct);
            pending = 0;
        }

        await db.SaveChangesAsync(ct);
    }

    private static void ApplyRow(
        Product product,
        ProductImportRow row,
        Dictionary<string, Category> categories,
        Dictionary<string, Publisher> publishers,
        Dictionary<string, Brand> brands,
        Dictionary<string, Author> authors,
        ImportReport report)
    {
        product.Name = Truncate(row.Name, 300);
        product.Description = Truncate(row.Description, 8000);
        product.Price = row.Price;
        product.DiscountedPrice = row.DiscountedPrice;
        product.Stock = SyntheticValues.StockFor(row.Sku);
        product.IsActive = true;
        product.DeletedAt = null;

        product.Category = categories[string.Join(PathSeparator, row.CategoryPath)];

        product.Publisher = Lookup(publishers, row.PublisherName);
        product.PublisherName = product.Publisher?.Name;

        product.Brand = Lookup(brands, row.BrandName);
        product.BrandName = product.Brand?.Name;

        ApplyAuthors(product, row, authors);
        ApplyImages(product, row, report);
        ApplyBookDetail(product, row, report);
    }

    private static TEntity? Lookup<TEntity>(Dictionary<string, TEntity> bySlug, string? name)
        where TEntity : class
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return bySlug.GetValueOrDefault(SlugGenerator.Generate(name));
    }

    private static void ApplyAuthors(
        Product product, ProductImportRow row, Dictionary<string, Author> authors)
    {
        // Kaynak tek doğru: satırdaki liste neyse bağlantılar o olur.
        product.ProductAuthors.Clear();

        var linked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new List<string>();

        foreach (var name in row.AuthorNames)
        {
            var slug = SlugGenerator.Generate(name);

            // "H. G. Wells" ve "H.G. Wells" aynı yazara çözülür; aynı ürüne
            // iki kez bağlanırsa bileşik anahtar patlar.
            if (slug.Length == 0 || !linked.Add(slug)) continue;
            if (!authors.TryGetValue(slug, out var author)) continue;

            product.ProductAuthors.Add(new ProductAuthor { Author = author });
            names.Add(author.Name);
        }

        product.AuthorNames = names.Count == 0 ? null : Truncate(string.Join(", ", names), 500);
    }

    private static void ApplyImages(Product product, ProductImportRow row, ImportReport report)
    {
        // Cloudinary'de barınan (admin'in yüklediği) görseller KORUNUR: kaynak
        // xlsx'te karşılıkları yok, düz Clear() onları sessizce siler ve
        // Cloudinary'de yetim bırakır — üstelik satır gittiği için haftalık
        // temizlik job'ı (ImageCleanupJobs) bir daha asla bulamaz (plan 2.6).
        foreach (var sourced in product.Images.Where(i => !i.IsMigrated).ToList())
            product.Images.Remove(sourced);

        // Kalan (varsa) hosted görsellerin sırasından devam et — yeni D&R
        // satırları onların ÖNÜNE geçmesin.
        var order = product.Images.Count == 0 ? 0 : product.Images.Max(i => i.DisplayOrder) + 1;
        var written = 0;

        foreach (var url in row.ImageUrls)
        {
            if (url.Length > 1000) continue;

            product.Images.Add(new ProductImage
            {
                SourceUrl = url,
                DisplayOrder = order++,
                // D&R görselleri KALICI OLARAK taşınmıyor (telif — Faz 10 kapsam
                // kararı). IsMigrated bu kaynaktan gelen satırlarda hep false.
                IsMigrated = false
            });
            written++;
        }

        report.ImagesWritten += written;
    }

    private static void ApplyBookDetail(Product product, ProductImportRow row, ImportReport report)
    {
        if (!row.IsBook)
        {
            // Ürün kitaplıktan çıkmışsa (kategori düzeltilmişse) detay da gitsin.
            product.BookDetail = null;
            return;
        }

        product.BookDetail ??= new BookDetail();
        product.BookDetail.Isbn = Truncate(row.Isbn, 20);
        product.BookDetail.PageCount = row.PageCount;
        product.BookDetail.Language = Truncate(row.Language, 50);
        product.BookDetail.PublishedYear = row.PublishedYear;
        product.BookDetail.Binding = row.Binding;

        report.BookDetailsWritten++;
    }

    /// Kolon limitleri kaynağın bugünkü halinde aşılmıyor (en uzun ad 132,
    /// en uzun açıklama 7525) ama sonraki scrape'te aşılırsa aktarım
    /// DbUpdateException ile ortasında patlamasın.
    [return: NotNullIfNotNull(nameof(value))]
    private static string? Truncate(string? value, int maxLength)
    {
        if (value is null) return null;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
