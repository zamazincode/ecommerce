using Commerce.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;

namespace Commerce.Api.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(180).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();

        // Kendine referanslı ilişki: Kitap → Roman → Polisiye
        b.HasOne(x => x.Parent)
         .WithMany(x => x.Children)
         .HasForeignKey(x => x.ParentId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PublisherConfiguration : IEntityTypeConfiguration<Publisher>
{
    public void Configure(EntityTypeBuilder<Publisher> b)
    {
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(220).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
    }
}

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> b)
    {
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(220).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
    }
}

public class AuthorConfiguration : IEntityTypeConfiguration<Author>
{
    public void Configure(EntityTypeBuilder<Author> b)
    {
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(220).IsRequired();
        b.Property(x => x.Bio).HasMaxLength(4000);
        b.HasIndex(x => x.Slug).IsUnique();
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.Property(x => x.Sku).HasMaxLength(64);
        b.Property(x => x.Name).HasMaxLength(300).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(320).IsRequired();
        b.Property(x => x.Description).HasMaxLength(8000);
        b.Property(x => x.AuthorNames).HasMaxLength(500);
        b.Property(x => x.PublisherName).HasMaxLength(200);
        b.Property(x => x.BrandName).HasMaxLength(200);

        // Hesaplanan property'ler veritabanına yazılmaz
        b.Ignore(x => x.EffectivePrice);
        b.Ignore(x => x.IsInStock);

        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => x.CategoryId);
        b.HasIndex(x => x.CreatedAt);
        b.HasIndex(x => x.Price);

        // Filtreli unique: elle eklenen ürünlerin SKU'su null olabilir ve
        // Postgres'te birden fazla NULL zaten çakışmaz — filtre yine de
        // niyeti açık yazıyor ve indeksi küçültüyor.
        b.HasIndex(x => x.Sku).IsUnique().HasFilter("\"Sku\" IS NOT NULL");

        // Stok yarışına karşı optimistic concurrency.
        // Postgres'in her satırda tuttuğu gizli xmin sistem kolonunu sürüm
        // damgası olarak kullanır — ekstra kolon eklemeye gerek yok,
        // migration'a da bir şey yazılmaz.
        //
        // Npgsql'in convention'ı şu üç koşulu sağlayan property'yi otomatik
        // olarak xmin sistem kolonuna bağlar: uint + concurrency token +
        // OnAddOrUpdate. Kodda erişmeyeceğimiz için shadow property yeterli.
        b.Property<uint>("xmin")
         .IsConcurrencyToken()
         .ValueGeneratedOnAddOrUpdate();

        // Son savunma hattı: uygulama katmanı hata yapsa bile stok negatife düşmesin.
        b.ToTable(t => t.HasCheckConstraint(
            "ck_products_stock_non_negative", "\"Stock\" >= 0"));

        b.HasOne(x => x.Category)
         .WithMany(x => x.Products)
         .HasForeignKey(x => x.CategoryId)
         .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Publisher)
         .WithMany(x => x.Products)
         .HasForeignKey(x => x.PublisherId)
         .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.Brand)
         .WithMany(x => x.Products)
         .HasForeignKey(x => x.BrandId)
         .OnDelete(DeleteBehavior.SetNull);

        // ── Arama (Faz 4) ──────────────────────────────────────────
        // SearchVector/SearchName Commerce.Domain'e Npgsql bağımlılığı
        // eklememek için SHADOW property olarak tanımlanıyor (CLAUDE.md:
        // "Commerce.Domain saf POCO, EF'e bağımlı değil"). Uygulama kodu
        // bu iki kolonu hiç okumaz/yazmaz — veritabanı GENERATED (stored)
        // olarak kendisi üretir, sorgularda EF.Property<T> ile erişilir.
        //
        // unaccent ÖNCE, kök bulma (to_tsvector) SONRA uygulanıyor: aksansız
        // yazan kullanıcı ile aksanlı sorgu simetrik şekilde eşleşsin diye
        // (bkz. plan Ölçüm 2.3/2.4). immutable_unaccent, yerleşik unaccent()
        // IMMUTABLE olmadığı için migration'da tanımlanan bir sarmalayıcı.
        b.Property<NpgsqlTsVector>("SearchVector")
         .HasColumnType("tsvector")
         .HasComputedColumnSql(
             """
             setweight(to_tsvector('turkish', immutable_unaccent(coalesce("Name", ''))), 'A') ||
             setweight(to_tsvector('turkish', immutable_unaccent(coalesce("AuthorNames", ''))), 'B') ||
             setweight(to_tsvector('turkish', immutable_unaccent(coalesce("PublisherName", ''))), 'C') ||
             setweight(to_tsvector('turkish', immutable_unaccent(coalesce("Description", ''))), 'D')
             """,
             stored: true);

        b.HasIndex("SearchVector")
         .HasMethod("gin")
         .HasDatabaseName("ix_products_searchvector");

        // Autocomplete için: Ad + yazar adı (yayınevi BİLEREK yok — eklenirse
        // "can" yazan kullanıcının önerileri "Can Yayınları"nın onlarca
        // ürünüyle dolar). text kullanılıyor, varchar(300) DEĞİL: Name(300) +
        // AuthorNames(500) toplamı sığmaz, ayrıca ConfigureConventions'daki
        // 512 sınırını açıkça ezmemiz gerekiyor.
        b.Property<string>("SearchName")
         .HasColumnType("text")
         .HasComputedColumnSql(
             """immutable_unaccent(lower(coalesce("Name", '') || ' ' || coalesce("AuthorNames", '')))""",
             stored: true);

        b.HasIndex("SearchName")
         .HasMethod("gin")
         .HasOperators("gin_trgm_ops")
         .HasDatabaseName("ix_products_searchname_trgm");

        // Soft delete: silinmiş ürünler tüm sorgulardan otomatik düşer.
        // Bilerek görmek istersen .IgnoreQueryFilters() kullanırsın.
        b.HasQueryFilter(x => x.DeletedAt == null);
    }
}

public class ProductAuthorConfiguration : IEntityTypeConfiguration<ProductAuthor>
{
    public void Configure(EntityTypeBuilder<ProductAuthor> b)
    {
        b.HasKey(x => new { x.ProductId, x.AuthorId });

        b.HasOne(x => x.Product)
         .WithMany(x => x.ProductAuthors)
         .HasForeignKey(x => x.ProductId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Author)
         .WithMany(x => x.ProductAuthors)
         .HasForeignKey(x => x.AuthorId)
         .OnDelete(DeleteBehavior.Cascade);

        // Product'ın soft delete filtresiyle eşleşen filtre.
        // Olmazsa silinmiş ürünün yazar bağlantıları sorgularda görünmeye devam eder.
        b.HasQueryFilter(x => x.Product.DeletedAt == null);
    }
}

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> b)
    {
        b.Property(x => x.SourceUrl).HasMaxLength(1000).IsRequired();
        b.Property(x => x.CloudinaryPublicId).HasMaxLength(300);

        b.HasIndex(x => new { x.ProductId, x.DisplayOrder });
        b.HasIndex(x => x.IsMigrated);   // Faz 10: geçmemişleri hızlı bulmak için

        b.HasOne(x => x.Product)
         .WithMany(x => x.Images)
         .HasForeignKey(x => x.ProductId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasQueryFilter(x => x.Product.DeletedAt == null);
    }
}

public class BookDetailConfiguration : IEntityTypeConfiguration<BookDetail>
{
    public void Configure(EntityTypeBuilder<BookDetail> b)
    {
        b.HasKey(x => x.ProductId);        // 1-1: PK aynı zamanda FK
        b.Property(x => x.Isbn).HasMaxLength(20);
        b.Property(x => x.Language).HasMaxLength(50);
        b.HasIndex(x => x.Isbn);           // Faz M3: barkoddan ürün bulma

        b.HasOne(x => x.Product)
         .WithOne(x => x.BookDetail)
         .HasForeignKey<BookDetail>(x => x.ProductId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasQueryFilter(x => x.Product.DeletedAt == null);
    }
}