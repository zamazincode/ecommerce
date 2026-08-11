using Commerce.Api.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Api.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityId).HasMaxLength(100);
        b.Property(x => x.Action).HasMaxLength(20).IsRequired();

        // ConfigureConventions tüm string'lere 512 sınırı veriyor; jsonb kolon
        // tipi bu sınırı geçersiz kılıyor (uzunluk kolona yansımıyor).
        b.Property(x => x.OldValues).HasColumnType("jsonb");
        b.Property(x => x.NewValues).HasColumnType("jsonb");

        b.HasIndex(x => new { x.EntityType, x.EntityId });
        b.HasIndex(x => x.CreatedAt);

        // FK YOK: kullanıcı silinse bile denetim kaydı ayakta kalmalı
        // (OrderItem.ProductId'nin FK'sız bırakılmasıyla aynı desen).
    }
}
