namespace Commerce.Domain.Catalog;

public class ProductImage
{
    public int Id { get; set; }
    public int ProductId { get; set; }

    /// Dış kaynaktan (D&R) gelen orijinal görsel URL'i. Cloudinary'de barınan
    /// görsellerde ise dönüşümsüz Cloudinary URL'i tutulur — kolon NOT NULL.
    public string SourceUrl { get; set; } = null!;

    /// Tam URL DEĞİL, sadece public_id (örn. "products/abc123").
    public string? CloudinaryPublicId { get; set; }

    public int DisplayOrder { get; set; }

    /// "Bu görsel Cloudinary'de barınıyor mu?" Faz 10'da D&R görsellerinin
    /// toplu geçişi TELİF gerekçesiyle İPTAL edildi; 2339 içe aktarılan
    /// görselde kalıcı olarak false. Yalnızca admin'in Cloudinary'ye yüklediği
    /// görsellerde true olur. Kolon adı geriye dönük uyumluluk için korundu
    /// (migration maliyetine değmiyor) — adı "geçmiş mi" çağrıştırıyor ama
    /// artık bir geçiş süreci yok, sadece "kaynak" bilgisi.
    public bool IsMigrated { get; set; }

    public Product Product { get; set; } = null!;
}