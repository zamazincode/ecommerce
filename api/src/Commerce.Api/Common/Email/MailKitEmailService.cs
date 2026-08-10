using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Commerce.Api.Common.Email;

/// Gerçek SMTP gönderimi (Faz 9). Dev'de Mailpit'e, prod'da yalnızca
/// yapılandırma değişerek gerçek bir sağlayıcıya bağlanır.
///
/// İstisna YUTULMAZ (kılavuzdan sapma): auth yolunda AuthService.TrySendEmailAsync
/// zaten hatayı yutuyor, ama job yolunda yutulursa Hangfire "başarılı" sanır ve
/// retry hiç çalışmaz.
public sealed class MailKitEmailService(
    IOptions<EmailSettings> options, ILogger<MailKitEmailService> logger) : IEmailService
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var settings = options.Value;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient
        {
            // SMTP donarsa bir Hangfire worker'ı süresiz tutmasın (K7).
            Timeout = settings.TimeoutSeconds * 1000
        };

        try
        {
            await client.ConnectAsync(
                settings.Host, settings.Port,
                settings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto, ct);

            if (!string.IsNullOrWhiteSpace(settings.Username))
                await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, ct);

            await client.SendAsync(message, ct);

            // Gövde LOGLANMAZ — şifre sıfırlama/doğrulama linki Seq'e düşmesin.
            logger.LogInformation("Mail gönderildi. Alıcı: {To}, Konu: {Subject}", to, subject);
        }
        finally
        {
            // Hiç bağlanılmamışken de güvenli — istisna atmıyor (ölçüldü).
            await client.DisconnectAsync(true, ct);
        }
    }
}
