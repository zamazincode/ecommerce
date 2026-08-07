using Commerce.Domain.Common;

namespace Commerce.Api.Features.Catalog.Dtos;

/// Liste/kart görünümü. Açıklama, ISBN, sayfa sayısı YOK.
/// 20 ürünün tam açıklamasını göndermek boşuna bant genişliği.
public sealed record ProductListDto(
    int Id,
    string Slug,
    string Name,
    string? AuthorNames,
    decimal Price,
    decimal? DiscountedPrice,
    decimal EffectivePrice,
    bool InStock,
    string? ImageUrl,
    int CategoryId);

public sealed record ProductDetailDto(
    int Id,
    string Slug,
    string Name,
    string? Description,
    decimal Price,
    decimal? DiscountedPrice,
    decimal EffectivePrice,
    int Stock,
    bool InStock,
    CategoryBriefDto Category,
    PublisherBriefDto? Publisher,
    BrandBriefDto? Brand,
    IReadOnlyList<AuthorBriefDto> Authors,
    BookDetailDto? BookDetail,
    IReadOnlyList<string> ImageUrls);

public sealed record CategoryBriefDto(int Id, string Name, string Slug);

public sealed record PublisherBriefDto(int Id, string Name, string Slug);

public sealed record BrandBriefDto(int Id, string Name, string Slug);

public sealed record AuthorBriefDto(int Id, string Name, string Slug);

public sealed record BookDetailDto(
    string? Isbn,
    int? PageCount,
    string? Language,
    int? PublishedYear,
    BookBinding Binding);
