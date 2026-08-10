using Commerce.Api.Persistence.Identity;
using Commerce.Domain.Orders;
using Commerce.Domain.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Commerce.Api.Persistence.Configurations;

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> b)
    {
        b.Property(x => x.CouponCode).HasMaxLength(50);

        // Bir kullanıcının en fazla bir sepeti olur.
        b.HasIndex(x => x.UserId).IsUnique();

        b.HasOne<ApplicationUser>()
         .WithMany()
         .HasForeignKey(x => x.UserId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> b)
    {
        // Aynı ürün sepette iki satır olamaz — miktar artar.
        b.HasIndex(x => new { x.CartId, x.ProductId }).IsUnique();

        b.HasOne(x => x.Cart)
         .WithMany(x => x.Items)
         .HasForeignKey(x => x.CartId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Product)
         .WithMany()
         .HasForeignKey(x => x.ProductId)
         .OnDelete(DeleteBehavior.Cascade);

        // Silinmiş ürün sepette görünmesin.
        b.HasQueryFilter(x => x.Product.DeletedAt == null);
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.Property(x => x.OrderNumber).HasMaxLength(30).IsRequired();
        b.Property(x => x.CouponCode).HasMaxLength(50);
        b.Property(x => x.IdempotencyKey).HasMaxLength(64);
        b.Property(x => x.Note).HasMaxLength(500);

        b.HasIndex(x => x.OrderNumber).IsUnique();
        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.CreatedAt);
        b.HasIndex(x => x.Status);

        // Aynı kullanıcı + aynı anahtar = tek sipariş. Uygulama katmanı hata
        // yapsa da ikinci kayıt oluşamaz. Filtreli: anahtar göndermeyen
        // istekler kısıtlamaya takılmaz (Postgres'te çoklu NULL zaten çakışmaz,
        // filtre indeksi de küçültüyor — Sku'daki kalıbın aynısı).
        b.HasIndex(x => new { x.UserId, x.IdempotencyKey })
         .IsUnique()
         .HasFilter("\"IdempotencyKey\" IS NOT NULL");

        // Owned type: ayrı tablo değil, Orders tablosunda
        // ShippingAddress_FullName gibi kolonlar olarak durur.
        b.OwnsOne(x => x.ShippingAddress, a =>
        {
            a.Property(p => p.FullName).HasMaxLength(200).IsRequired();
            a.Property(p => p.Phone).HasMaxLength(30).IsRequired();
            a.Property(p => p.City).HasMaxLength(100).IsRequired();
            a.Property(p => p.District).HasMaxLength(100).IsRequired();
            a.Property(p => p.FullAddress).HasMaxLength(1000).IsRequired();
        });

        b.HasOne<ApplicationUser>()
         .WithMany()
         .HasForeignKey(x => x.UserId)
         .OnDelete(DeleteBehavior.Restrict);   // Kullanıcı silinse bile sipariş kalsın
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.Property(x => x.ProductNameSnapshot).HasMaxLength(300).IsRequired();
        b.Property(x => x.ProductSlugSnapshot).HasMaxLength(320).IsRequired();

        b.Ignore(x => x.LineTotal);
        b.HasIndex(x => x.ProductId);

        b.HasOne(x => x.Order)
         .WithMany(x => x.Items)
         .HasForeignKey(x => x.OrderId)
         .OnDelete(DeleteBehavior.Cascade);

        // ProductId'ye FK constraint KOYMUYORUZ.
        // Ürün soft-delete edilse veya ileride gerçekten silinse bile
        // geçmiş sipariş satırı ayakta kalmalı. Bilgi zaten snapshot'ta.
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.Property(x => x.Provider).HasMaxLength(50).IsRequired();
        b.Property(x => x.ProviderTransactionId).HasMaxLength(200);
        b.Property(x => x.ProviderReference).HasMaxLength(200);   // konvansiyon 512 verirdi
        b.Property(x => x.RawResponse).HasMaxLength(20000);

        // Idempotency'nin veritabanı seviyesindeki garantisi:
        // aynı işlem iki kez kaydedilemez. Webhook'lar tekrar tekrar gelir.
        b.HasIndex(x => x.ProviderTransactionId)
         .IsUnique()
         .HasFilter("\"ProviderTransactionId\" IS NOT NULL");

        // Callback tek bir ödeme kaydını FirstOrDefaultAsync ile bulmalı —
        // aynı token iki satıra bağlanamamalı.
        b.HasIndex(x => x.ProviderReference)
         .IsUnique()
         .HasFilter("\"ProviderReference\" IS NOT NULL");

        b.HasOne(x => x.Order)
         .WithMany(x => x.Payments)
         .HasForeignKey(x => x.OrderId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> b)
    {
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
    }
}

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> b)
    {
        b.Property(x => x.Comment).HasMaxLength(2000);

        b.ToTable(t => t.HasCheckConstraint(
            "ck_reviews_rating_range", "\"Rating\" BETWEEN 1 AND 5"));

        // Bir kullanıcı bir ürüne bir kez yorum yapar.
        b.HasIndex(x => new { x.ProductId, x.UserId }).IsUnique();
        b.HasIndex(x => x.IsApproved);

        b.HasOne(x => x.Product)
         .WithMany()
         .HasForeignKey(x => x.ProductId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasOne<ApplicationUser>()
         .WithMany()
         .HasForeignKey(x => x.UserId)
         .OnDelete(DeleteBehavior.Cascade);

        // Silinmiş ürünün yorumları listelerde çıkmasın.
        b.HasQueryFilter(x => x.Product.DeletedAt == null);
    }
}

public class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> b)
    {
        b.HasKey(x => new { x.UserId, x.ProductId });

        b.HasOne(x => x.Product)
         .WithMany()
         .HasForeignKey(x => x.ProductId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasOne<ApplicationUser>()
         .WithMany()
         .HasForeignKey(x => x.UserId)
         .OnDelete(DeleteBehavior.Cascade);

        // Silinmiş ürün favorilerde görünmesin.
        b.HasQueryFilter(x => x.Product.DeletedAt == null);
    }
}

public class SearchLogConfiguration : IEntityTypeConfiguration<SearchLog>
{
    public void Configure(EntityTypeBuilder<SearchLog> b)
    {
        b.Property(x => x.Term).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Term);
        b.HasIndex(x => x.CreatedAt);
    }
}