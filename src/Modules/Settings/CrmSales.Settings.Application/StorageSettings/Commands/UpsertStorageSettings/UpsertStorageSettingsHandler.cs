using CrmSales.Settings.Domain.Repositories;
using CrmSales.SharedKernel;
using StorageSettingsEntity = CrmSales.Settings.Domain.Entities.StorageSettings;

namespace CrmSales.Settings.Application.StorageSettings.Commands.UpsertStorageSettings;

public static class UpsertStorageSettingsHandler
{
    public static async Task<Result> Handle(
        UpsertStorageSettingsCommand command,
        IStorageSettingsRepository repo,
        CancellationToken ct)
    {
        var existing = await repo.GetAsync(ct);
        if (existing is null)
        {
            var settings = StorageSettingsEntity.Create(command.MaxFileSizeBytes, command.MaxFilesPerOrder);
            await repo.AddAsync(settings, ct);
        }
        else
        {
            existing.Update(command.MaxFileSizeBytes, command.MaxFilesPerOrder);
            await repo.UpdateAsync(existing, ct);
        }
        return Result.Success();
    }
}
