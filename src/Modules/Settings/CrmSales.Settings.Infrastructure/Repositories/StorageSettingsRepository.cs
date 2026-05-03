using CrmSales.Settings.Domain.Entities;
using CrmSales.Settings.Domain.Repositories;
using CrmSales.Settings.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Settings.Infrastructure.Repositories;

internal sealed class StorageSettingsRepository(SettingsDbContext db) : IStorageSettingsRepository
{
    public Task<StorageSettings?> GetAsync(CancellationToken ct)
        => db.StorageSettings.FirstOrDefaultAsync(ct);

    public async Task AddAsync(StorageSettings settings, CancellationToken ct)
    {
        db.StorageSettings.Add(settings);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(StorageSettings settings, CancellationToken ct)
    {
        db.StorageSettings.Update(settings);
        await db.SaveChangesAsync(ct);
    }
}
