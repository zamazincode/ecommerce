using Commerce.Api.Common.Results;
using Commerce.Api.Features.Catalog.Dtos;

namespace Commerce.Api.Features.Catalog;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products").WithTags("Products");

        group.MapGet("/", GetProducts)
             .WithSummary("Ürünleri sayfalı listeler")
             .WithDescription(
                 "Kategori, yazar, yayınevi, fiyat aralığı ve stok durumuna göre filtrelenebilir.")
             .Produces<PagedResult<ProductListDto>>();

        // URL'de id değil SLUG: /urun/suc-ve-ceza hem Google hem insan için daha iyi.
        group.MapGet("/{slug}", GetProductBySlug)
             .WithSummary("Slug ile ürün detayı")
             .Produces<ProductDetailDto>()
             .ProducesProblem(StatusCodes.Status404NotFound);

        // Burada id kullanıyoruz — detay sayfasından çağrılıyor, SEO'ya girmiyor.
        group.MapGet("/{id:int}/related", GetRelated)
             .WithSummary("Aynı kategoriden 8 ürün")
             .Produces<IReadOnlyList<ProductListDto>>()
             .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/categories").WithTags("Categories");

        group.MapGet("/", async (CategoryService service, CancellationToken ct)
                => await service.GetFlatAsync(ct))
             .WithSummary("Tüm kategoriler (düz liste)")
             .Produces<IReadOnlyList<CategoryDto>>();

        group.MapGet("/tree", async (CategoryService service, CancellationToken ct)
                => await service.GetTreeAsync(ct))
             .WithSummary("Hiyerarşik kategori ağacı")
             .Produces<IReadOnlyList<CategoryTreeDto>>();

        group.MapGet("/{slug}/products", GetProductsByCategory)
             .WithSummary("Kategorinin ürünleri (alt kategoriler dahil)")
             .Produces<PagedResult<ProductListDto>>()
             .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/authors/{slug}", async (
                string slug, CatalogService service, CancellationToken ct)
                => await service.GetAuthorBySlugAsync(slug, ct))
           .WithTags("Catalog")
           .WithSummary("Yazar detayı")
           .Produces<AuthorDetailDto>()
           .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/publishers", async (
                CatalogService service, CancellationToken ct)
                => await service.GetPublishersAsync(ct))
           .WithTags("Catalog")
           .WithSummary("Yayınevi listesi")
           .Produces<IReadOnlyList<PublisherBriefDto>>();

        app.MapGet("/api/brands", async (
                CatalogService service, CancellationToken ct)
                => await service.GetBrandsAsync(ct))
           .WithTags("Catalog")
           .WithSummary("Marka listesi")
           .Produces<IReadOnlyList<BrandBriefDto>>();

        app.MapGet("/api/home", async (
                CatalogService service, CancellationToken ct)
                => await service.GetHomeAsync(ct))
           .WithTags("Catalog")
           .WithSummary("Ana sayfa blokları")
           .Produces<HomeDto>();

        return app;
    }

    // ─────────────────────────────────────────────────────────
    // Handler'lar
    // ─────────────────────────────────────────────────────────
    private static async Task<PagedResult<ProductListDto>> GetProducts(
        [AsParameters] ProductFilterRequest filter,
        ProductService service,
        CancellationToken ct)
        => await service.SearchAsync(filter, ct);

    private static async Task<ProductDetailDto> GetProductBySlug(
        string slug, ProductService service, CancellationToken ct)
        => await service.GetBySlugAsync(slug, ct);

    private static async Task<IReadOnlyList<ProductListDto>> GetRelated(
        int id, ProductService service, CancellationToken ct)
        => await service.GetRelatedAsync(id, ct);

    private static async Task<PagedResult<ProductListDto>> GetProductsByCategory(
        string slug,
        [AsParameters] ProductFilterRequest filter,
        CategoryService categories,
        ProductService products,
        CancellationToken ct)
    {
        var category = await categories.GetBySlugAsync(slug, ct);   // yoksa 404
        return await products.SearchAsync(filter with { CategoryId = category.Id }, ct);
    }
}
