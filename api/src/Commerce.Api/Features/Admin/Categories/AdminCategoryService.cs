using Commerce.Api.Common.Caching;
using Commerce.Api.Common.Exceptions;
using Commerce.Api.Features.Catalog;
using Commerce.Api.Persistence;
using Commerce.Domain.Catalog;
using Commerce.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Commerce.Api.Features.Admin.Categories;

public sealed class AdminCategoryService(AppDbContext db, CategoryService categories, HybridCache cache)
{
    public async Task<IReadOnlyList<AdminCategoryDto>> ListAsync(CancellationToken ct = default)
        => await db.Categories.AsNoTracking()
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .Select(c => new AdminCategoryDto(
                c.Id, c.Name, c.Slug, c.ParentId, c.DisplayOrder, c.IsActive, c.Products.Count))
            .ToListAsync(ct);

    public async Task<AdminCategoryDto> CreateAsync(
        CreateCategoryRequest request, CancellationToken ct = default)
    {
        if (request.ParentId is { } parentId)
        {
            var exists = await db.Categories.AnyAsync(c => c.Id == parentId, ct);
            if (!exists) throw new BusinessRuleException("Belirtilen üst kategori bulunamadı.");
        }

        var slug = await GenerateUniqueSlugAsync(request.Name, ct);

        var category = new Category
        {
            Name = request.Name.Trim(),
            Slug = slug,
            ParentId = request.ParentId,
            DisplayOrder = request.DisplayOrder ?? 0
        };

        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);

        // Ürün filtresi GetSelfAndDescendantIdsAsync üzerinden kategori cache'ini
        // kullanıyor (2.9) — kategori yazmaları iki etiketi de temizlemeli.
        await cache.RemoveByTagAsync(CacheTags.Categories, ct);
        await cache.RemoveByTagAsync(CacheTags.Products, ct);

        return new AdminCategoryDto(
            category.Id, category.Name, category.Slug, category.ParentId,
            category.DisplayOrder, category.IsActive, ProductCount: 0);
    }

    public async Task<AdminCategoryDto> UpdateAsync(
        int id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw NotFoundException.For("Kategori", id);

        if (request.ParentId is { } parentId)
        {
            if (parentId == id)
                throw new BusinessRuleException("Bir kategori kendi üst kategorisi olamaz.");

            var parentExists = await db.Categories.AnyAsync(c => c.Id == parentId, ct);
            if (!parentExists)
                throw new BusinessRuleException("Belirtilen üst kategori bulunamadı.");

            // K11: çok seviyeli döngü koruması. Kontrolü YAZMADAN ÖNCE yapıp
            // SONRA cache'i temizlemek doğru sıra — tersi yarış hâlinde bayat
            // ağacı geri doldururdu (5.7).
            var descendants = await categories.GetSelfAndDescendantIdsAsync(id, ct);
            if (descendants.Contains(parentId))
                throw new BusinessRuleException(
                    "Bir kategori kendi alt kategorisinin altına taşınamaz.");
        }

        category.Name = request.Name.Trim();
        category.ParentId = request.ParentId;
        category.DisplayOrder = request.DisplayOrder ?? category.DisplayOrder;
        category.IsActive = request.IsActive;

        await db.SaveChangesAsync(ct);

        await cache.RemoveByTagAsync(CacheTags.Categories, ct);
        await cache.RemoveByTagAsync(CacheTags.Products, ct);

        var productCount = await db.Products.CountAsync(p => p.CategoryId == id, ct);
        return new AdminCategoryDto(
            category.Id, category.Name, category.Slug, category.ParentId,
            category.DisplayOrder, category.IsActive, productCount);
    }

    private async Task<string> GenerateUniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = SlugGenerator.Generate(name);
        if (string.IsNullOrEmpty(baseSlug)) baseSlug = "kategori";

        var candidate = baseSlug;
        var suffix = 2;
        while (await db.Categories.AnyAsync(c => c.Slug == candidate, ct))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }
}
