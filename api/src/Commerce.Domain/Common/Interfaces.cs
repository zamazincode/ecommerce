namespace Commerce.Domain.Common;

/// Oluşturma/güncelleme zamanı otomatik doldurulacak entity'ler.
public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}

/// Gerçekten silinmeyen, sadece işaretlenen entity'ler.
public interface ISoftDeletable
{
    DateTime? DeletedAt { get; set; }
}