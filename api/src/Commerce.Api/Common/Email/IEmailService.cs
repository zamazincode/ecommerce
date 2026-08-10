namespace Commerce.Api.Common.Email;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}

/// Faz 9'da MailKitEmailService devraldı; SMTP'siz ortamlar için duruyor.
public sealed class ConsoleEmailService(ILogger<ConsoleEmailService> logger) : IEmailService
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[MAIL] Alıcı: {To} | Konu: {Subject}\n{Body}", to, subject, htmlBody);
        return Task.CompletedTask;
    }
}
