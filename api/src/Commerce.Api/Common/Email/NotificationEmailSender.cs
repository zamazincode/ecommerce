using System.Globalization;

namespace Commerce.Api.Common.Email;

/// Hangi şablon + hangi konu satırı gideceğine karar veren katman.
/// AuthService ve job'lar IEmailService'i doğrudan çağırmaz — buradan geçer.
public sealed class NotificationEmailSender(IEmailService email, EmailTemplateRenderer renderer)
{
    // Para tr-TR ile METNE çevrilir; Scriban'ın sayı biçimlendirmesi invariant
    // (Scriban 7.2.6 ölçüldü) — "1,234.50" Türkçe mailde yanlış olurdu.
    private static readonly CultureInfo Tr = new("tr-TR");

    internal static string Para(decimal tutar) => tutar.ToString("N2", Tr) + " ₺";

    public Task SendEmailConfirmationAsync(
        string to, string ad, string dogrulamaUrl, CancellationToken ct = default)
    {
        const string subject = "E-posta adresinizi doğrulayın";
        var body = renderer.RenderWithLayout("eposta-dogrula",
            new { Ad = ad, DogrulamaUrl = dogrulamaUrl }, subject);
        return email.SendAsync(to, subject, body, ct);
    }

    public Task SendPasswordResetAsync(
        string to, string ad, string sifirlamaUrl, CancellationToken ct = default)
    {
        const string subject = "Şifre sıfırlama";
        var body = renderer.RenderWithLayout("sifre-sifirla",
            new { Ad = ad, SifirlamaUrl = sifirlamaUrl }, subject);
        return email.SendAsync(to, subject, body, ct);
    }

    public Task SendOrderConfirmationAsync(
        string to, string ad, OrderMailModel siparis, CancellationToken ct = default)
    {
        var subject = $"Siparişiniz alındı — {siparis.SiparisNo}";
        var body = renderer.RenderWithLayout("siparis-onay", new
        {
            Ad = ad,
            SiparisNo = siparis.SiparisNo,
            Kalemler = siparis.Kalemler.Select(k => new
            {
                Ad = k.Ad,
                Adet = k.Adet,
                Tutar = Para(k.Tutar)
            }),
            AraToplam = Para(siparis.AraToplam),
            Indirim = Para(siparis.Indirim),
            Kargo = Para(siparis.Kargo),
            Toplam = Para(siparis.Toplam),
            SiparisUrl = siparis.SiparisUrl
        }, subject);
        return email.SendAsync(to, subject, body, ct);
    }

    public Task SendShippedAsync(
        string to, string ad, string siparisNo, string siparisUrl, CancellationToken ct = default)
    {
        var subject = $"Siparişiniz kargoya verildi — {siparisNo}";
        var body = renderer.RenderWithLayout("kargoya-verildi",
            new { Ad = ad, SiparisNo = siparisNo, SiparisUrl = siparisUrl }, subject);
        return email.SendAsync(to, subject, body, ct);
    }

    public Task SendCancelledAsync(
        string to, string ad, string siparisNo, string siparisUrl, CancellationToken ct = default)
    {
        var subject = $"Siparişiniz iptal edildi — {siparisNo}";
        var body = renderer.RenderWithLayout("siparis-iptal",
            new { Ad = ad, SiparisNo = siparisNo, SiparisUrl = siparisUrl }, subject);
        return email.SendAsync(to, subject, body, ct);
    }

    public Task SendCartReminderAsync(
        string to, string ad, IReadOnlyList<string> urunAdlari, string sepetUrl, CancellationToken ct = default)
    {
        const string subject = "Sepetinizde ürünler sizi bekliyor";
        var body = renderer.RenderWithLayout("sepet-hatirlatma",
            new { Ad = ad, Urunler = urunAdlari, SepetUrl = sepetUrl }, subject);
        return email.SendAsync(to, subject, body, ct);
    }
}

/// Job'ın topladığı veriyi sender'a taşır — anonim tip kullanmıyoruz ki
/// unit testte de aynı model kurulabilsin.
public sealed record OrderMailModel(
    string SiparisNo,
    IReadOnlyList<OrderMailLine> Kalemler,
    decimal AraToplam, decimal Indirim, decimal Kargo, decimal Toplam,
    string SiparisUrl);

public sealed record OrderMailLine(string Ad, int Adet, decimal Tutar);
