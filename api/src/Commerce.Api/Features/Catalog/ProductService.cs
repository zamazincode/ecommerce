using System.Linq.Expressions;
using Commerce.Api.Common.Caching;
using Commerce.Api.Common.Exceptions;
using Commerce.Api.Common.Extensions;
using Commerce.Api.Common.Results;
using Commerce.Api.Features.Catalog.Dtos;
using Commerce.Api.Persistence;
using Commerce.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Commerce.Api.Features.Catalog;

public sealed class ProductService(
    AppDbContext db,
    HybridCache cache,
    CategoryService categories)
{
    /// Projeksiyon ifadesi. EF bunu SQL'e çevirir; SADECE bu kolonlar SELECT edilir.
    ///
    /// DİKKAT: Burada p.EffectivePrice YAZAMAZSIN. O, veritabanında olmayan bir
    /// C# property'si — EF çeviremez, "could not be translated" hatası verir.
    /// Aynı hesabı ifade içinde açıkça yazıyoruz.
    private static readonly Expression<Func<Product, ProductListDto>> ToListDto =
        p => new ProductListDto(
            p.Id,
            p.Slug,
            p.Name,
            p.AuthorNames,
            p.Price,
            p.DiscountedPrice,
            p.DiscountedPrice ?? p.Price,
            p.Stock > 0,
            p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.SourceUrl).FirstOrDefault(),
            p.CategoryId);

    public async Task<PagedResult<ProductListDto>> SearchAsync(
        ProductFilterRequest filter, CancellationToken ct = default)
    {
        var page = filter.ToPageRequest();

        IReadOnlyCollection<int>? categoryIds = null;
        if (filter.CategoryId is { } categoryId)
            categoryIds = await categories.GetSelfAndDescendantIdsAsync(categoryId, ct);

        var minPrice = filter.MinPrice;
        var maxPrice = filter.MaxPrice;
        var authorId = filter.AuthorId;
        var publisherId = filter.PublisherId;

        var query = db.Products
            .AsNoTracking()                       // Okuma yapıyoruz; change tracker boşuna çalışmasın
            .Where(p => p.IsActive)
            .WhereIf(categoryIds is not null, p => categoryIds!.Contains(p.CategoryId))
            .WhereIf(publisherId.HasValue, p => p.PublisherId == publisherId)
            .WhereIf(authorId.HasValue, p => p.ProductAuthors.Any(pa => pa.AuthorId == authorId))
            // Fiyat filtresi KULLANICININ ÖDEYECEĞİ fiyata göre.
            // "50-100₺" seçen biri indirimli 80₺'lik ürünü görmeli.
            .WhereIf(minPrice.HasValue, p => (p.DiscountedPrice ?? p.Price) >= minPrice!.Value)
            .WhereIf(maxPrice.HasValue, p => (p.DiscountedPrice ?? p.Price) <= maxPrice!.Value)
            .WhereIf(filter.InStock == true, p => p.Stock > 0);

        query = ProductSorting.ApplySort(query, filter.SortBy, filter.SortDir);

        return await query.Select(ToListDto).ToPagedResultAsync(page, ct);
    }

    public async Task<ProductDetailDto> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var cached = await cache.GetOrCreateAsync(
            CacheKeys.Product(slug),
            (Service: this, Slug: slug),
            static async (state, token) => await state.Service.LoadDetailAsync(state.Slug, token),
            new HybridCacheEntryOptions { Expiration = CacheDurations.ProductDetail },
            tags: [CacheTags.Products],
            cancellationToken: ct);

        if (cached is null)
            throw NotFoundException.For("Ürün", slug);

        // STOK CACHE'LENMEZ. Ürün detayını 10 dakika cache'liyoruz ama
        // "son 1 adet" yazan sayfada stok 10 dakika eski olamaz.
        // Tek kolonluk bu sorgu çok ucuz.
        var stock = await db.Products
            .AsNoTracking()
            .Where(p => p.Id == cached.Id)
            .Select(p => p.Stock)
            .FirstOrDefaultAsync(ct);

        // record + with: cache'teki nesneyi MUTASYONA UĞRATMADAN kopyasını üretir.
        // L1 katmanı aynı referansı paylaştığı için mutasyon çok tehlikeli olurdu.
        return cached with { Stock = stock, InStock = stock > 0 };
    }

    private async Task<ProductDetailDto?> LoadDetailAsync(string slug, CancellationToken ct)
        => await db.Products
            .AsNoTracking()
            .Where(p => p.Slug == slug && p.IsActive)
            .Select(p => new ProductDetailDto(
                p.Id,
                p.Slug,
                p.Name,
                p.Description,
                p.Price,
                p.DiscountedPrice,
                p.DiscountedPrice ?? p.Price,
                0,      // Stock — cache'e girmesin, çağıran taraf dolduruyor
                false,  // InStock — aynı sebep
                new CategoryBriefDto(p.Category.Id, p.Category.Name, p.Category.Slug),
                p.Publisher == null
                    ? null
                    : new PublisherBriefDto(p.Publisher.Id, p.Publisher.Name, p.Publisher.Slug),
                p.Brand == null
                    ? null
                    : new BrandBriefDto(p.Brand.Id, p.Brand.Name, p.Brand.Slug),
                p.ProductAuthors
                    .Select(pa => new AuthorBriefDto(pa.Author.Id, pa.Author.Name, pa.Author.Slug))
                    .ToList(),
                p.BookDetail == null
                    ? null
                    : new BookDetailDto(
                        p.BookDetail.Isbn,
                        p.BookDetail.PageCount,
                        p.BookDetail.Language,
                        p.BookDetail.PublishedYear,
                        p.BookDetail.Binding),
                p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.SourceUrl).ToList()))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ProductListDto>> GetRelatedAsync(
        int productId, CancellationToken ct = default)
    {
        var categoryId = await db.Products
            .AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => (int?)p.CategoryId)
            .FirstOrDefaultAsync(ct);

        if (categoryId is null)
            throw NotFoundException.For("Ürün", productId);

        return await db.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.CategoryId == categoryId && p.Id != productId)
            .OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
            .Take(8)
            .Select(ToListDto)
            .ToListAsync(ct);
    }

    /// Ana sayfa blokları için ortak yardımcılar — CatalogService kullanıyor.
    ///
    /// DİKKAT: Buradan ENTITY sorgusu dönüyor, DTO değil. Projeksiyonu
    /// çağıran taraf EN SONDA uyguluyor. Önce projekte edip sonra
    /// OrderBy/Where yazarsan EF sıralamayı çeviremez ve şu hatayı alırsın:
    /// "The LINQ expression ... OrderByDescending(p => new ProductListDto(...))
    ///  could not be translated."
    internal IQueryable<Product> ActiveProducts()
        => db.Products.AsNoTracking().Where(p => p.IsActive);

    /// Projeksiyon ifadesini paylaşıyoruz ki liste kolonları tek yerde kalsın.
    internal static Expression<Func<Product, ProductListDto>> ListProjection => ToListDto;
}
