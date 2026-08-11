using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Commerce.Api.Persistence.Seeding.Import;

/// "dotnet run -- import [dosya] [--keep]" komutunun gövdesi.
/// Program.cs'i şişirmemek için ayrı dosyada.
public static class CatalogImportCommand
{
    /// Dosya yolu verilmezse buraya bakılır (proje klasörüne göreli).
    private const string DefaultRelativePath = "Persistence/Seeding/urunler.xlsx";

    public static async Task RunAsync(
        IServiceProvider services,
        IHostEnvironment environment,
        string[] args,
        CancellationToken ct = default)
    {
        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(ct);

        var filePath = ResolveFilePath(environment, args);
        var purge = !args.Contains("--keep");

        Console.WriteLine($"Kaynak    : {filePath}");
        Console.WriteLine(purge
            ? "Mod       : katalog temizlenip yeniden yüklenecek"
            : "Mod       : mevcut katalog korunacak, SKU üzerinden upsert");

        IReadOnlyList<ImportRawRow> rows;
        try
        {
            rows = ExcelWorkbookReader.Read(filePath);
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException)
        {
            Console.Error.WriteLine($"HATA: {ex.Message}");
            Environment.ExitCode = 1;
            return;
        }

        var logger = services.GetRequiredService<ILogger<CatalogImporter>>();
        var importer = new CatalogImporter(db, logger);
        var report = await importer.ImportAsync(
            rows,
            new ImportOptions { PurgeCatalog = purge, NowUtc = DateTime.UtcNow },
            ct);

        Console.WriteLine(report);
        Console.WriteLine($"Veritabanındaki ürün sayısı: {await db.Products.CountAsync(ct)}");
    }

    /// "import" kelimesinden sonraki, tire ile başlamayan ilk argüman dosya yoludur.
    private static string ResolveFilePath(IHostEnvironment environment, string[] args)
    {
        var index = Array.IndexOf(args, "import");

        for (var i = index + 1; i < args.Length; i++)
        {
            if (args[i].StartsWith('-')) continue;
            return Path.GetFullPath(args[i]);
        }

        return Path.Combine(environment.ContentRootPath, DefaultRelativePath);
    }
}
