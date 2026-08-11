using System.Linq.Expressions;
using Commerce.Api.Common.Caching;
using Commerce.Api.Common.Exceptions;
using Commerce.Api.Common.Extensions;
using Commerce.Api.Common.Images;
using Commerce.Api.Common.Results;
using Commerce.Api.Features.Admin.Products.Dtos;
using Commerce.Api.Features.Catalog;
using Commerce.Api.Persistence;
using Commerce.Domain.Catalog;
using Commerce.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Commerce.Api.Features.Admin.Products;

public sealed class AdminProductService(
    AppDbContext db, HybridCache cache, ProductImageUrls urls,
    TimeProvider clock, ILogger<AdminProductService> logger)
{
    /// DİKKAT: p.EffectivePrice / urls.Build(...) burada ÇAĞRILAMAZ — EF bir
    /// metot çağrısını SQL'e çeviremez (ProductService.ToListDto'daki uyarının
    /// aynısı). Yalnızca hazır önekler (ThumbnailPrefix) `+` ile birleştirilir.
    private static Expression<Func<Product, AdminProductListDto>> ToListDto(ProductImageUrls urls) =>
        p => new AdminProductListDto(
            p.Id, p.Slug, p.Name, p.Sku, p.Price, p.DiscountedPrice, p.Stock, p.IsActive, p.DeletedAt,
            p.CategoryId, p.Category.Name,
            p.Images.OrderBy(i => i.DisplayOrder)
                .Select(i => i.IsMigrated && i.CloudinaryPublicId != null
                    ? urls.ThumbnailPrefix + i.CloudinaryPublicId
                    : i.SourceUrl)
                .FirstOrDefault(),
            p.CreatedAt);

    public async Task<PagedResult<AdminProductListDto>> SearchAsync(
        AdminProductFilterRequest filter, CancellationToken ct = default)
    {
        var q = string.IsNullOrWhiteSpace(filter.Q) ? null : filter.Q.Trim();

        // includeDeleted=true → IgnoreQueryFilters (K12): admin panelinde
        // pasif/silinmiş ürünler de görünebilmeli (Faz W5'in ihtiyacı).
        var query = filter.IncludeDeleted == true
            ? db.Products.IgnoreQueryFilters().AsNoTracking()
            : db.Products.AsNoTracking();

        query = query
            .WhereIf(filter.CategoryId.HasValue, p => p.CategoryId == filter.CategoryId!.Value)
            .WhereIf(filter.IsActive.HasValue, p => p.IsActive == filter.IsActive!.Value)
            .WhereIf(q is not null, p => EF.Functions.ILike(p.Name, $"%{q}%") || p.Sku == q);

        query = ProductSorting.ApplySort(query, filter.SortBy, filter.SortDir);

        // Filtrele → sırala → sayfala → EN SONDA Select (CLAUDE.md kuralı).
        return await query.Select(ToListDto(urls)).ToPagedResultAsync(filter.ToPageRequest(), ct);
    }

    public async Task<AdminProductDetailDto> GetAsync(int id, CancellationToken ct = default)
        => await db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new AdminProductDetailDto(
                p.Id, p.Slug, p.Name, p.Sku, p.Description,
                p.Price, p.DiscountedPrice, p.Stock, p.IsActive, p.DeletedAt,
                p.CategoryId, p.Category.Name,
                p.PublisherId, p.Publisher == null ? null : p.Publisher.Name,
                p.BrandId, p.Brand == null ? null : p.Brand.Name,
                p.CreatedAt, p.UpdatedAt))
            .FirstOrDefaultAsync(ct)
            ?? throw NotFoundException.For("Ürün", id);

    public async Task<AdminProductDetailDto> CreateAsync(
        CreateProductRequest request, CancellationToken ct = default)
    {
        // Kategori/yayınevi/marka VARLIĞI kontrol edilir — FK ihlaliyle 500 almak
        // yerine düzgün bir 400. Yayınevi/marka adı denormalize kolona kopyalanır:
        // aksi hâlde SearchVector eksik üretilir, yeni ürün yayınevi adıyla
        // aranınca bulunamaz (K10, CatalogConfigurations.cs:123-132).
        var categoryExists = await db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct);
        if (!categoryExists)
            throw new BusinessRuleException("Belirtilen kategori bulunamadı.");

        string? publisherName = null;
        if (request.PublisherId is { } publisherId)
        {
            publisherName = await db.Publishers
                .Where(p => p.Id == publisherId).Select(p => p.Name).FirstOrDefaultAsync(ct)
                ?? throw new BusinessRuleException("Belirtilen yayınevi bulunamadı.");
        }

        string? brandName = null;
        if (request.BrandId is { } brandId)
        {
            brandName = await db.Brands
                .Where(b => b.Id == brandId).Select(b => b.Name).FirstOrDefaultAsync(ct)
                ?? throw new BusinessRuleException("Belirtilen marka bulunamadı.");
        }

        var sku = string.IsNullOrWhiteSpace(request.Sku) ? null : request.Sku.Trim();
        if (sku is not null)
        {
            // Filtreli unique index var (Sku IS NOT NULL) — kontrol edilmezse 500 (K10).
            var skuTaken = await db.Products.IgnoreQueryFilters().AnyAsync(p => p.Sku == sku, ct);
            if (skuTaken) throw new ConflictException($"'{sku}' SKU'su zaten kullanımda.");
        }

        var slug = await GenerateUniqueSlugAsync(request.Name, ct);

        var product = new Product
        {
            Sku = sku,
            Name = request.Name.Trim(),
            Slug = slug,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Price = request.Price,
            DiscountedPrice = request.DiscountedPrice,
            Stock = request.Stock,
            CategoryId = request.CategoryId,
            PublisherId = request.PublisherId,
            BrandId = request.BrandId,
            PublisherName = publisherName,
            BrandName = brandName,
            // AuthorNames bu fazda hiç doldurulmuyor — yazar CRUD'u yok (§12 borcu).
            IsActive = request.IsActive ?? true,
            CreatedAt = clock.GetUtcNow().UtcDateTime
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);

        await cache.RemoveByTagAsync(CacheTags.Products, ct);

        logger.LogInformation("Admin ürün oluşturdu: {ProductId} ({Slug})", product.Id, product.Slug);

        return await GetAsync(product.Id, ct);
    }

    public async Task<AdminProductDetailDto> UpdateAsync(
        int id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw NotFoundException.For("Ürün", id);

        var categoryExists = await db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct);
        if (!categoryExists)
            throw new BusinessRuleException("Belirtilen kategori bulunamadı.");

        string? publisherName = null;
        if (request.PublisherId is { } publisherId)
        {
            publisherName = await db.Publishers
                .Where(p => p.Id == publisherId).Select(p => p.Name).FirstOrDefaultAsync(ct)
                ?? throw new BusinessRuleException("Belirtilen yayınevi bulunamadı.");
        }

        string? brandName = null;
        if (request.BrandId is { } brandId)
        {
            brandName = await db.Brands
                .Where(b => b.Id == brandId).Select(b => b.Name).FirstOrDefaultAsync(ct)
                ?? throw new BusinessRuleException("Belirtilen marka bulunamadı.");
        }

        // Slug'a DOKUNULMUYOR: ürün adı değişse bile URL kırılmasın
        // (import'un "eşleşen ürünün slug'ı korunur" kararıyla aynı gerekçe).
        product.Name = request.Name.Trim();
        product.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        product.Price = request.Price;
        product.DiscountedPrice = request.DiscountedPrice;
        product.CategoryId = request.CategoryId;
        product.PublisherId = request.PublisherId;
        product.BrandId = request.BrandId;
        product.PublisherName = publisherName;
        product.BrandName = brandName;
        product.IsActive = request.IsActive;
        product.UpdatedAt = clock.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(ct);
        await cache.RemoveByTagAsync(CacheTags.Products, ct);

        return await GetAsync(id, ct);
    }

    /// Takipli soft delete (K3): ExecuteUpdateAsync KULLANILMAZ — SaveChanges
    /// boru hattını atlar, hem denetim interceptor'ı hem xmin devreden çıkar.
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw NotFoundException.For("Ürün", id);

        product.DeletedAt = clock.GetUtcNow().UtcDateTime;
        product.IsActive = false;

        await db.SaveChangesAsync(ct);
        await cache.RemoveByTagAsync(CacheTags.Products, ct);
    }

    public async Task<AdminProductDetailDto> RestoreAsync(int id, CancellationToken ct = default)
    {
        var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw NotFoundException.For("Ürün", id);

        product.DeletedAt = null;
        // IsActive'i AÇMIYORUZ — admin ürünü kontrol edip yayına kendisi alsın (K3).
        await db.SaveChangesAsync(ct);
        await cache.RemoveByTagAsync(CacheTags.Products, ct);

        return await GetAsync(id, ct);
    }

    /// Takipli okuma + SaveChanges (K9). DbUpdateConcurrencyException BİLEREK
    /// yakalanmıyor: GlobalExceptionHandler onu zaten 409'a çeviriyor.
    public async Task<AdminProductDetailDto> UpdateStockAsync(
        int id, int stock, CancellationToken ct = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw NotFoundException.For("Ürün", id);

        product.Stock = stock;
        await db.SaveChangesAsync(ct);
        await cache.RemoveByTagAsync(CacheTags.Products, ct);

        return await GetAsync(id, ct);
    }

    /// K8: Açık BeginTransactionAsync YOK — tek SaveChangesAsync zaten kendi
    /// transaction'ında atomik (ölçüldü: bir satır CHECK ihlal ederse hepsi
    /// geri alınıyor). xmin çakışması istisnası yutulmuyor → 409.
    public async Task<BulkPriceUpdateResult> BulkUpdatePriceAsync(
        IReadOnlyList<BulkPriceUpdateItem> items, CancellationToken ct = default)
    {
        var ids = items.Select(i => i.ProductId).ToList();

        var products = await db.Products
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var missing = ids.Where(id => !products.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            throw new BusinessRuleException(
                $"Şu ürün(ler) bulunamadı, HİÇBİR fiyat güncellenmedi: {string.Join(", ", missing)}.");

        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var item in items)
        {
            var product = products[item.ProductId];
            product.Price = item.Price;
            product.DiscountedPrice = item.DiscountedPrice;
            product.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        await cache.RemoveByTagAsync(CacheTags.Products, ct);

        return new BulkPriceUpdateResult(products.Count);
    }

    /// Çakışırsa "-2", "-3" … eklenir. IgnoreQueryFilters: silinmiş bir ürünün
    /// slug'ı da rezerve kalır — OrderItem.ProductSlugSnapshot'taki eski
    /// linkler yeni bir ürünle karışmasın (K10).
    private async Task<string> GenerateUniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = SlugGenerator.Generate(name);
        if (string.IsNullOrEmpty(baseSlug)) baseSlug = "urun";

        var candidate = baseSlug;
        var suffix = 2;
        while (await db.Products.IgnoreQueryFilters().AnyAsync(p => p.Slug == candidate, ct))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }
}
