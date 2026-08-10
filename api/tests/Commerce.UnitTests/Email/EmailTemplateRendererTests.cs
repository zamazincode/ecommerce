using Commerce.Api.Common.Email;
using Shouldly;

namespace Commerce.UnitTests.Email;

public class EmailTemplateRendererTests
{
    private static EmailTemplateRenderer CreateRenderer() => new();

    // Embedded resource adı doğru, Scriban sözdizimi geçerli olmalı.
    // Bir şablon .csproj'a girmezse (EmbeddedResource glob'unu kaçırırsa) burada patlar.
    [Theory]
    [InlineData("_layout")]
    [InlineData("eposta-dogrula")]
    [InlineData("sifre-sifirla")]
    [InlineData("siparis-onay")]
    [InlineData("kargoya-verildi")]
    [InlineData("sepet-hatirlatma")]
    public void AllTemplates_Parse(string templateName)
    {
        var renderer = CreateRenderer();

        // Layout'un kendi placeholder'ları ({{ govde }}), diğerlerinin kendi
        // değişkenleri var — hepsine boş/örnek bir model vermek yeterli, amaç
        // sadece Template.Parse'ın hata vermemesi.
        var model = new
        {
            Baslik = "test", Govde = "test",
            Ad = "Test", DogrulamaUrl = "http://x", SifirlamaUrl = "http://x",
            SiparisNo = "ORD-1", Kalemler = new[] { new { Ad = "Kitap", Adet = 1, Tutar = "10,00 ₺" } },
            AraToplam = "10,00 ₺", Indirim = "0,00 ₺", Kargo = "0,00 ₺", Toplam = "10,00 ₺",
            SiparisUrl = "http://x", Urunler = new[] { "Kitap" }, SepetUrl = "http://x"
        };

        Should.NotThrow(() => renderer.Render(templateName, model));
    }

    [Fact]
    public void Render_SiparisOnay_SubstitutesVariables()
    {
        var renderer = CreateRenderer();
        var model = new
        {
            // 'ç' karakteri kasıtlı olarak KULLANILMADI: html.escape onu
            // &#231; olarak sayısal varlığa çeviriyor (ölçüldü) — bu test
            // ham ikame doğruluğunu kontrol ediyor, kaçış davranışını değil
            // (o ayrı bir testte, EscapesHtmlInProductName).
            Ad = "Ayşe Yılmaz",
            SiparisNo = "ORD-20260810-AAAAAA",
            Kalemler = new[] { new { Ad = "Simyacı", Adet = 2, Tutar = "419,80 ₺" } },
            AraToplam = "419,80 ₺", Indirim = "0,00 ₺", Kargo = "0,00 ₺", Toplam = "419,80 ₺",
            SiparisUrl = "http://localhost:3000/hesabim/siparisler/ORD-20260810-AAAAAA"
        };

        var result = renderer.Render("siparis-onay", model);

        result.ShouldContain("Ayşe Yılmaz");
        result.ShouldContain("ORD-20260810-AAAAAA");
        result.ShouldContain("Simyacı");
        result.ShouldContain("419,80 ₺");
        // Scriban 7.2.6'da bilinmeyen değişken sessizce boş string üretir; bu
        // yüzden hatalı bir değişken adı çıplak {{ }} bırakmaz. Yine de bunu
        // kilitliyoruz — bir şablon değişikliğinde yazım hatası sessiz kalmasın.
        result.ShouldNotContain("{{");
        result.ShouldNotContain("}}");
    }

    [Fact]
    public void Render_SiparisOnay_EscapesHtmlInProductName()
    {
        var renderer = CreateRenderer();
        var model = new
        {
            Ad = "Test",
            SiparisNo = "ORD-1",
            Kalemler = new[] { new { Ad = "<b>Suç & Ceza</b>", Adet = 1, Tutar = "10,00 ₺" } },
            AraToplam = "10,00 ₺", Indirim = "0,00 ₺", Kargo = "0,00 ₺", Toplam = "10,00 ₺",
            SiparisUrl = "http://x"
        };

        var result = renderer.Render("siparis-onay", model);

        // Scriban HTML kaçışı YAPMIYOR — şablon | html.escape kullanmak zorunda.
        result.ShouldNotContain("<b>Suç");
        result.ShouldContain("&lt;b&gt;");
        result.ShouldContain("&amp;");
    }

    [Fact]
    public void Render_DoesNotEscapeUrls()
    {
        var renderer = CreateRenderer();
        var url = "http://localhost:3000/eposta-dogrula?email=a%40b.com&token=xyz";
        var model = new { Ad = "Test", DogrulamaUrl = url };

        var result = renderer.Render("eposta-dogrula", model);

        // URL'ler kaçışlanırsa & -> &amp; olur, QueryHelpers.ParseQuery ikinci
        // parametreyi "amp;token" diye okur — iki mevcut auth testini kırardı.
        result.ShouldContain(url);
        result.ShouldNotContain("&amp;token");
    }

    [Fact]
    public void Render_UnknownTemplate_Throws()
    {
        var renderer = CreateRenderer();

        Should.Throw<InvalidOperationException>(() => renderer.Render("hic-yok", new { }));
    }

    [Fact]
    public void RenderWithLayout_WrapsBodyAndKeepsHtml()
    {
        var renderer = CreateRenderer();
        var model = new { Ad = "Test", DogrulamaUrl = "http://x/dogrula?a=1&b=2" };

        var result = renderer.RenderWithLayout("eposta-dogrula", model, "Başlık");

        result.ShouldContain("<html");
        result.ShouldContain("http://x/dogrula?a=1&b=2");

        // {{ govde }}'den önce hiçbir <a href> olmamalı — ExtractLink (auth
        // testleri) İLK href'i bağlantı sanıyor.
        var firstHrefIndex = result.IndexOf("href=\"", StringComparison.Ordinal);
        var firstBodyMarker = result.IndexOf("dogrula?a=1", StringComparison.Ordinal);
        firstHrefIndex.ShouldBeGreaterThanOrEqualTo(0);
        firstBodyMarker.ShouldBeGreaterThanOrEqualTo(0);
        // İlk href, gövdedeki doğrulama linkinin KENDİSİ olmalı (layout'ta
        // ondan önce başka bir <a href> yok).
        (firstHrefIndex <= firstBodyMarker).ShouldBeTrue();
    }

    [Fact]
    public void Render_TwiceWithDifferentModels_DoesNotLeakState()
    {
        var renderer = CreateRenderer();

        var first = renderer.Render("eposta-dogrula", new { Ad = "Birinci", DogrulamaUrl = "http://x/1" });
        var second = renderer.Render("eposta-dogrula", new { Ad = "İkinci", DogrulamaUrl = "http://x/2" });

        first.ShouldContain("Birinci");
        first.ShouldNotContain("İkinci");
        second.ShouldContain("İkinci");
        second.ShouldNotContain("Birinci");
    }
}
