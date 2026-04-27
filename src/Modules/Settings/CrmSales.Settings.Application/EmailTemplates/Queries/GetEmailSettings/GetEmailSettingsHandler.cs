using CrmSales.Settings.Application.EmailTemplates.DTOs;
using CrmSales.Settings.Domain.Repositories;
using CrmSales.SharedKernel;

namespace CrmSales.Settings.Application.EmailTemplates.Queries.GetEmailSettings;

public static class GetEmailSettingsHandler
{
    public static async Task<Result<EmailSettingsDto>> Handle(
        GetEmailSettingsQuery query,
        IEmailSettingsRepository repo,
        CancellationToken ct)
    {
        var settings = await repo.GetAsync(ct);
        if (settings is null)
            return Result.Success(new EmailSettingsDto(null, "", 587, "", "", "", true, false, false));

        return Result.Success(new EmailSettingsDto(
            settings.Id, settings.Host, settings.Port, settings.Username,
            settings.FromName, settings.FromAddress, settings.EnableSsl, settings.IsEnabled,
            HasPassword: !string.IsNullOrWhiteSpace(settings.Password),
            AuthMode: settings.AuthMode));
    }
}
