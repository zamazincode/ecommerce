using System.Collections.Concurrent;
using Commerce.Api.Common.Email;

namespace Commerce.IntegrationTests.Infrastructure;

public sealed record SentEmail(string To, string Subject, string Body);

/// Fake — mock değil. Çalışan basit bir implementasyon; gönderilenleri listeye ekler.
/// Tekrar tekrar kullanacağın bağımlılıklar için fake yaz (PLAN.md 3.10).
public sealed class FakeEmailService : IEmailService
{
    public ConcurrentBag<SentEmail> SentEmails { get; } = [];

    /// "Mail patlarsa damga atılmamalı" testi için (K4'ün sırasını kilitler).
    public bool ThrowOnSend { get; set; }

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (ThrowOnSend) throw new InvalidOperationException("SMTP hatası (test).");

        SentEmails.Add(new SentEmail(to, subject, htmlBody));
        return Task.CompletedTask;
    }

    public void Clear() => SentEmails.Clear();
}
