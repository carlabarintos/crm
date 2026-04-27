using CrmSales.Settings.Application.Services;
using CrmSales.Settings.Domain.Entities;
using CrmSales.Settings.Domain.Repositories;
using CrmSales.SharedKernel;

namespace CrmSales.Settings.Application.EmailTemplates.Commands.SaveEmailSettings;

public static class SaveEmailSettingsHandler
{
    public static async Task<Result> Handle(
        SaveEmailSettingsCommand command,
        IEmailSettingsRepository repo,
        IEncryptionService encryption,
        CancellationToken ct)
    {
        var existing = await repo.GetAsync(ct);
        if (existing is null)
        {
            var encryptedPassword = string.IsNullOrEmpty(command.Password)
                ? string.Empty
                : encryption.Encrypt(command.Password);

            var settings = EmailSettings.Create(
                command.Host, command.Port, command.Username, encryptedPassword,
                command.FromName, command.FromAddress, command.EnableSsl, command.AuthMode);
            await repo.AddAsync(settings, ct);
        }
        else
        {
            var encryptedPassword = string.IsNullOrEmpty(command.Password)
                ? null
                : encryption.Encrypt(command.Password);

            existing.Update(
                command.Host, command.Port, command.Username, encryptedPassword,
                command.FromName, command.FromAddress, command.EnableSsl, command.IsEnabled,
                command.AuthMode);
            await repo.UpdateAsync(existing, ct);
        }
        return Result.Success();
    }
}
