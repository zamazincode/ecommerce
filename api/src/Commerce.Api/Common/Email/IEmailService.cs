namespace Commerce.Api.Common.Email;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}

/// Faz 9'a kadar geçerli. Maili konsola ve log'a yazar, dışarı hiçbir şey gitmez.
public sealed class ConsoleEmailService(ILogger<ConsoleEmailService> logger) : IEmailService
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[MAIL] Alıcı: {To} | Konu: {Subject}\n{Body}", to, subject, htmlBody);
        return Task.CompletedTask;
    }
}
