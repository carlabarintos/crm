using CrmSales.Settings.Application.Services;
using CrmSales.Settings.Domain.Repositories;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace CrmSales.Settings.Infrastructure.Services;

internal sealed class SmtpEmailService(
    IEmailSettingsRepository settingsRepo,
    IEncryptionService encryption,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    public async Task SendAsync(string toEmail, string toName, string subject, string bodyHtml, CancellationToken ct = default)
    {
        var settings = await settingsRepo.GetAsync(ct);
        if (settings is null || !settings.IsEnabled)
        {
            logger.LogInformation("Email skipped — SMTP is not configured or disabled.");
            return;
        }

        var plaintextPassword = string.IsNullOrEmpty(settings.Password)
            ? string.Empty
            : encryption.Decrypt(settings.Password);

#pragma warning disable SYSLIB0045
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
            Credentials = string.IsNullOrWhiteSpace(settings.Username)
                ? null
                : new NetworkCredential(settings.Username, plaintextPassword)
        };
#pragma warning restore SYSLIB0045

        var from = new MailAddress(settings.FromAddress, settings.FromName);
        var to = new MailAddress(toEmail, string.IsNullOrWhiteSpace(toName) ? toEmail : toName);

        using var message = new MailMessage(from, to)
        {
            Subject = subject,
            Body = bodyHtml,
            IsBodyHtml = true
        };

        await client.SendMailAsync(message, ct);
        logger.LogInformation("Email sent to {ToEmail} — subject: {Subject}", toEmail, subject);
    }
}
