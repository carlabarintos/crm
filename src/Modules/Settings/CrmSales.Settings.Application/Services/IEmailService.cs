namespace CrmSales.Settings.Application.Services;

public interface IEmailService
{
    Task SendAsync(string toEmail, string toName, string subject, string bodyHtml, CancellationToken ct = default);
}
