using System.Collections.Concurrent;
using System.Reflection;
using Scriban;

namespace Commerce.Api.Common.Email;

/// Şablonu bulur, parse eder, cache'ler, render eder. Singleton: parse edilmiş
/// Template nesneleri sabit — model her çağrıda değişiyor, şablon değişmiyor.
public sealed class EmailTemplateRenderer
{
    private readonly ConcurrentDictionary<string, Template> _cache = new();
    private static readonly Assembly Assembly = typeof(EmailTemplateRenderer).Assembly;

    public string Render(string templateName, object model)
    {
        var template = _cache.GetOrAdd(templateName, LoadTemplate);
        // Scriban 7.2.6'da string.ToSnakeCase() uzantısı YOK. Varsayılan renamer
        // (StandardMemberRenamer) MusteriAdi -> musteri_adi dönüşümünü zaten yapıyor,
        // renamer'ı hiç vermeye gerek yok.
        return template.Render(model);
    }

    /// Gövdeyi ortak iskelete sarar. Layout ayrı bir şablon; Scriban'ın
    /// include/TemplateLoader mekanizmasına gerek yok — iki render yeter.
    /// Scriban HTML kaçışı YAPMADIĞI için {{ govde }} ham HTML olarak basılır.
    public string RenderWithLayout(string templateName, object model, string baslik)
        => Render("_layout", new { Baslik = baslik, Govde = Render(templateName, model) });

    private static Template LoadTemplate(string name)
    {
        var resourceName = $"Commerce.Api.Common.Email.Templates.{name}.html.sbn";
        using var stream = Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Mail şablonu bulunamadı: {resourceName}");
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();

        var template = Template.Parse(text, resourceName);
        if (template.HasErrors)
            throw new InvalidOperationException(
                $"Mail şablonu '{name}' derlenemedi: {string.Join(" | ", template.Messages)}");

        return template;
    }
}
