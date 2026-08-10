using System.Globalization;
using Commerce.Api.Common.Email;
using NSubstitute;
using Shouldly;

namespace Commerce.UnitTests.Email;

public class NotificationEmailSenderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (NotificationEmailSender Sender, IEmailService Email) Create()
    {
        var email = Substitute.For<IEmailService>();
        var sender = new NotificationEmailSender(email, new EmailTemplateRenderer());
        return (sender, email);
    }

    private static OrderMailModel SampleOrder() => new(
        "ORD-20260810-AAAAAA",
        [new OrderMailLine("Suç ve Ceza", 2, 1234.50m)],
        1234.50m, 0m, 0m, 1234.50m,
        "http://localhost:3000/hesabim/siparisler/ORD-20260810-AAAAAA");

    /// Sender'ın gönderdiği gövdeyi yakalamak için: Arg.Do çağrısı gerçek
    /// çağrıdan ÖNCE kurulur ve eşleşen her SendAsync çağrısında tetiklenir.
    private static (NotificationEmailSender Sender, Func<string> Body) CreateCapturing()
    {
        var email = Substitute.For<IEmailService>();
        var body = string.Empty;
        email.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Do<string>(b => body = b), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var sender = new NotificationEmailSender(email, new EmailTemplateRenderer());
        return (sender, () => body);
    }

    [Fact]
    public async Task SendOrderConfirmationAsync_PutsOrderNumberInSubject()
    {
        var (sender, email) = Create();

        await sender.SendOrderConfirmationAsync("ali@test.com", "Ali", SampleOrder(), Ct);

        await email.Received(1).SendAsync(
            "ali@test.com",
            Arg.Is<string>(s => s != null && s.Contains("ORD-20260810-AAAAAA")),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendOrderConfirmationAsync_FormatsMoneyInTurkish()
    {
        var (sender, body) = CreateCapturing();

        await sender.SendOrderConfirmationAsync("ali@test.com", "Ali", SampleOrder(), Ct);

        body().ShouldContain("1.234,50 ₺");
        body().ShouldNotContain("1,234.50");
    }

    [Fact]
    public async Task SendOrderConfirmationAsync_UnderInvariantCulture_StillFormatsTurkish()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            var (sender, body) = CreateCapturing();

            await sender.SendOrderConfirmationAsync("ali@test.com", "Ali", SampleOrder(), Ct);

            body().ShouldContain("1.234,50");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task SendEmailConfirmationAsync_UsesExactLegacySubject()
    {
        var (sender, email) = Create();

        await sender.SendEmailConfirmationAsync("ali@test.com", "Ali", "http://x/dogrula", Ct);

        // Faz 5'teki metinle BİREBİR aynı olmalı (AuthEndpointTests bu metni arıyor).
        await email.Received(1).SendAsync(
            "ali@test.com", "E-posta adresinizi doğrulayın", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendPasswordResetAsync_SubjectContainsSifre()
    {
        var (sender, email) = Create();

        await sender.SendPasswordResetAsync("ali@test.com", "Ali", "http://x/sifirla", Ct);

        await email.Received(1).SendAsync(
            "ali@test.com", Arg.Is<string>(s => s != null && s.Contains("Şifre")), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendPasswordResetAsync_FirstHrefIsTheResetLink()
    {
        var (sender, body) = CreateCapturing();

        await sender.SendPasswordResetAsync(
            "ali@test.com", "Ali", "http://x/sifirla?email=a%40b.com&token=xyz", Ct);

        var html = body();
        var start = html.IndexOf("href=\"", StringComparison.Ordinal) + "href=\"".Length;
        var end = html.IndexOf('"', start);
        html[start..end].ShouldBe("http://x/sifirla?email=a%40b.com&token=xyz");
    }

    [Fact]
    public async Task SendCartReminderAsync_ListsAllProductNames()
    {
        var (sender, body) = CreateCapturing();

        // 'ç' bilerek kullanılmadı: html.escape onu &#231; yapıyor (ölçüldü) —
        // bu test tüm ürünlerin listelendiğini kontrol ediyor, kaçışı değil.
        await sender.SendCartReminderAsync(
            "ali@test.com", "Ali", ["Suc ve Ceza", "1984", "Simyacı"], "http://x/sepet", Ct);

        body().ShouldContain("Suc ve Ceza");
        body().ShouldContain("1984");
        body().ShouldContain("Simyacı");
    }
}
