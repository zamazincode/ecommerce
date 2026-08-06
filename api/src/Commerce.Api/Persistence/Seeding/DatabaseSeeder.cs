using Bogus;
using Commerce.Api.Persistence.Identity;
using Commerce.Domain.Catalog;
using Commerce.Domain.Common;
using Commerce.Domain.Orders;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Api.Persistence.Seeding;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        await SeedRolesAndAdminAsync(services);

        if (await db.Products.AnyAsync())
        {
            Console.WriteLine("Ürünler zaten yüklü, seed atlandı.");
            return;
        }

        // Sabit tohum: her çalıştırmada AYNI veri üretilir.
        Randomizer.Seed = new Random(20260803);

        var categories = SeedCategories(db);
        var publishers = SeedPublishers(db);
        var authors = SeedAuthors(db);
        await db.SaveChangesAsync();

        SeedProducts(db, categories, publishers, authors);
        SeedCoupons(db);
        await db.SaveChangesAsync();

        Console.WriteLine($"Seed tamam: {await db.Products.CountAsync()} ürün.");
    }

    private static async Task SeedRolesAndAdminAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in new[] { AppRoles.Customer, AppRoles.Admin })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new ApplicationRole { Name = role });
        }

        const string adminEmail = "admin@commerce.local";
        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FirstName = "Sistem",
                LastName = "Yöneticisi",
                CreatedAt = DateTime.UtcNow
            };
            // Bu şifre sadece geliştirme içindir. Prod'da bu seed çalışmayacak.
            await userManager.CreateAsync(admin, "Admin123!");
            await userManager.AddToRoleAsync(admin, AppRoles.Admin);
        }
    }

    private static List<Category> SeedCategories(AppDbContext db)
    {
        var tree = new (string Parent, string[] Children)[]
        {
            ("Kitap",       ["Roman", "Polisiye", "Bilim Kurgu", "Tarih", "Felsefe", "Çocuk"]),
            ("Kırtasiye",   ["Defter", "Kalem", "Ajanda"]),
            ("Hobi",        ["Puzzle", "Kutu Oyunu"])
        };

        var all = new List<Category>();
        var order = 0;

        foreach (var (parentName, children) in tree)
        {
            var parent = new Category
            {
                Name = parentName,
                Slug = SlugGenerator.Generate(parentName),
                DisplayOrder = order++
            };
            db.Categories.Add(parent);
            all.Add(parent);

            var childOrder = 0;
            foreach (var childName in children)
            {
                var child = new Category
                {
                    Name = childName,
                    Slug = SlugGenerator.Generate(childName),
                    Parent = parent,
                    DisplayOrder = childOrder++
                };
                db.Categories.Add(child);
                all.Add(child);
            }
        }

        return all;
    }

    private static List<Publisher> SeedPublishers(AppDbContext db)
    {
        string[] names =
        [
            "Can Yayınları", "İletişim Yayınları", "Yapı Kredi Yayınları",
            "İş Bankası Kültür Yayınları", "Everest Yayınları", "Doğan Kitap",
            "Metis Yayınları", "Ayrıntı Yayınları", "Alfa Yayınları", "Kırmızı Kedi"
        ];

        var list = names.Select(n => new Publisher
        {
            Name = n,
            Slug = SlugGenerator.Generate(n)
        }).ToList();

        db.Publishers.AddRange(list);
        return list;
    }

    private static List<Author> SeedAuthors(AppDbContext db)
    {
        string[] names =
        [
            "Fyodor Dostoyevski", "Sabahattin Ali", "Orhan Pamuk", "Yaşar Kemal",
            "Ahmet Hamdi Tanpınar", "Oğuz Atay", "George Orwell", "Franz Kafka",
            "Albert Camus", "Haruki Murakami", "Stefan Zweig", "Jose Saramago",
            "Ursula K. Le Guin", "Isaac Asimov", "Halide Edib Adıvar",
            "Zülfü Livaneli", "Elif Şafak", "Hakan Günday", "Tolstoy", "Çehov"
        ];

        var list = names.Select(n => new Author
        {
            Name = n,
            Slug = SlugGenerator.Generate(n),
            Bio = $"{n} hakkında kısa biyografi metni."
        }).ToList();

        db.Authors.AddRange(list);
        return list;
    }

    private static void SeedProducts(
        AppDbContext db,
        List<Category> categories,
        List<Publisher> publishers,
        List<Author> authors)
    {
        string[] titles =
        [
            "Suç ve Ceza", "Kürk Mantolu Madonna", "Kara Kitap", "İnce Memed",
            "Huzur", "Tutunamayanlar", "1984", "Dönüşüm", "Yabancı",
            "İmkânsızın Şarkısı", "Satranç", "Körlük", "Yerdeniz Büyücüsü",
            "Vakıf", "Sinekli Bakkal", "Serenad", "Aşk", "Kinyas ve Kayra",
            "Anna Karenina", "Martı", "Beyaz Geceler", "Sefiller", "Fareler ve İnsanlar",
            "Bülbülü Öldürmek", "Cesur Yeni Dünya", "Hayvan Çiftliği", "Şeker Portakalı",
            "Küçük Prens", "Simyacı", "Momo"
        ];

        // Basılan farklı baskılar/ciltler → 30 başlık × 20 varyant = 600 ürün
        string[] editionSuffixes =
        [
            "", "(Ciltli)", "(Ciltsiz)", "(Özel Baskı)", "(50. Yıl Özel)",
            "(Cep Boy)", "(Büyük Boy)", "(Resimli)", "(Şömizli)", "(Kutulu)",
            "(Klasikler Serisi)", "(Yeni Çeviri)", "(Açıklamalı)", "(Tam Metin)",
            "(Sadeleştirilmiş)", "(Hediye Baskı)", "(İmzalı Baskı)", "(2. Hamur)",
            "(1. Hamur)", "(Bez Ciltli)"
        ];

        var leafCategories = categories.Where(c => c.Parent is not null).ToList();
        var faker = new Faker("tr");
        var products = new List<Product>();
        var usedSlugs = new HashSet<string>();

        foreach (var title in titles)
        {
            foreach (var suffix in editionSuffixes)
            {
                var name = string.IsNullOrEmpty(suffix) ? title : $"{title} {suffix}";
                var slug = SlugGenerator.Generate(name);

                // Slug unique olmak zorunda — çakışırsa sayı ekle.
                var baseSlug = slug;
                var counter = 2;
                while (!usedSlugs.Add(slug))
                    slug = $"{baseSlug}-{counter++}";

                var price = Math.Round(faker.Random.Decimal(45m, 480m), 2);
                var hasDiscount = faker.Random.Bool(0.35f);
                var author = faker.PickRandom(authors);
                var publisher = faker.PickRandom(publishers);

                var product = new Product
                {
                    Name = name,
                    Slug = slug,
                    Description = string.Join(" ", faker.Lorem.Paragraphs(2)),
                    Price = price,
                    DiscountedPrice = hasDiscount
                        ? Math.Round(price * faker.Random.Decimal(0.6m, 0.9m), 2)
                        : null,
                    Stock = faker.Random.Bool(0.1f) ? 0 : faker.Random.Int(1, 120),
                    Category = faker.PickRandom(leafCategories),
                    Publisher = publisher,
                    AuthorNames = author.Name,
                    PublisherName = publisher.Name,
                    IsActive = true,
                    CreatedAt = faker.Date.PastOffset(2).UtcDateTime,
                    ProductAuthors = [new ProductAuthor { Author = author }],
                    BookDetail = new BookDetail
                    {
                        Isbn = faker.Commerce.Ean13(),
                        PageCount = faker.Random.Int(80, 900),
                        Language = "Türkçe",
                        PublishedYear = faker.Random.Int(1960, 2025),
                        Binding = faker.PickRandom<BookBinding>()
                    },
                    Images =
                    [
                        new ProductImage
                        {
                            SourceUrl = $"https://picsum.photos/seed/{slug}/600/900",
                            DisplayOrder = 0,
                            IsMigrated = false
                        }
                    ]
                };

                products.Add(product);
            }
        }

        db.Products.AddRange(products);
    }

    private static void SeedCoupons(AppDbContext db)
    {
        var now = DateTime.UtcNow;
        db.Coupons.AddRange(
            new Coupon
            {
                Code = "HOSGELDIN10",
                Type = CouponType.Percentage,
                Value = 10m,
                MinCartTotal = 100m,
                ValidFrom = now.AddDays(-30),
                ValidTo = now.AddYears(1),
                UsageLimit = 1000
            },
            new Coupon
            {
                Code = "KITAP50",
                Type = CouponType.FixedAmount,
                Value = 50m,
                MinCartTotal = 300m,
                ValidFrom = now.AddDays(-10),
                ValidTo = now.AddMonths(3),
                UsageLimit = 200
            },
            new Coupon
            {
                Code = "SURESIGECMIS",
                Type = CouponType.Percentage,
                Value = 20m,
                MinCartTotal = 0m,
                ValidFrom = now.AddYears(-2),
                ValidTo = now.AddDays(-1),   // Test için: süresi geçmiş kupon
                UsageLimit = null
            });
    }
}