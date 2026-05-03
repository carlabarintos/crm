using CrmSales.Settings.Domain.Entities;

namespace CrmSales.Settings.Domain.Repositories;

public interface IStorageSettingsRepository
{
    Task<StorageSettings?> GetAsync(CancellationToken ct);
    Task AddAsync(StorageSettings settings, CancellationToken ct);
    Task UpdateAsync(StorageSettings settings, CancellationToken ct);
}
