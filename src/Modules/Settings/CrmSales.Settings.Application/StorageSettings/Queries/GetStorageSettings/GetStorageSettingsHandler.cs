using CrmSales.Settings.Application.StorageSettings.DTOs;
using CrmSales.Settings.Domain.Repositories;
using CrmSales.SharedKernel;
using StorageSettingsEntity = CrmSales.Settings.Domain.Entities.StorageSettings;

namespace CrmSales.Settings.Application.StorageSettings.Queries.GetStorageSettings;

public static class GetStorageSettingsHandler
{
    public static async Task<Result<StorageSettingsDto>> Handle(
        GetStorageSettingsQuery query,
        IStorageSettingsRepository repo,
        CancellationToken ct)
    {
        var s = await repo.GetAsync(ct);
        return Result.Success(new StorageSettingsDto(
            s?.MaxFileSizeBytes ?? StorageSettingsEntity.DefaultMaxFileSizeBytes,
            s?.MaxFilesPerOrder ?? StorageSettingsEntity.DefaultMaxFilesPerOrder,
            s?.UpdatedAt));
    }
}
