namespace Commerce.Domain.Catalog;

public class ProductImage
{
    public int Id { get; set; }
    public int ProductId { get; set; }

    /// Dış kaynaktan gelen orijinal görsel URL'i.
    /// Cloudinary geçişi tamamlanana kadar bu kullanılır.
    public string SourceUrl { get; set; } = null!;

    /// Tam URL DEĞİL, sadece public_id.
    public string? CloudinaryPublicId { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsMigrated { get; set; }

    public Product Product { get; set; } = null!;
}