using Commerce.Api.Persistence.Identity;
using Commerce.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Api.Persistence.Configurations;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> b)
    {
        b.Property(x => x.Title).HasMaxLength(50).IsRequired();
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(30).IsRequired();
        b.Property(x => x.City).HasMaxLength(100).IsRequired();
        b.Property(x => x.District).HasMaxLength(100).IsRequired();
        b.Property(x => x.FullAddress).HasMaxLength(1000).IsRequired();

        b.HasIndex(x => x.UserId);

        // Navigation property yok — FK'yi burada, tip parametresiyle kuruyoruz.
        b.HasOne<ApplicationUser>()
         .WithMany()
         .HasForeignKey(x => x.UserId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.Property(x => x.Token).HasMaxLength(200).IsRequired();
        b.Property(x => x.ReplacedByToken).HasMaxLength(200);

        b.HasIndex(x => x.Token).IsUnique();
        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.ExpiresAt);   // Faz 9: süresi geçmişleri temizleyen job

        b.Ignore(x => x.IsRevoked);

        b.HasOne<ApplicationUser>()
         .WithMany()
         .HasForeignKey(x => x.UserId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}