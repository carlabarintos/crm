using CrmSales.Settings.Domain.Entities;
using CrmSales.Settings.Domain.Repositories;
using CrmSales.Settings.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Settings.Infrastructure.Repositories;

internal sealed class EmailSettingsRepository(SettingsDbContext db) : IEmailSettingsRepository
{
    public Task<EmailSettings?> GetAsync(CancellationToken ct)
        => db.EmailSettings.FirstOrDefaultAsync(ct);

    public async Task AddAsync(EmailSettings settings, CancellationToken ct)
    {
        db.EmailSettings.Add(settings);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(EmailSettings settings, CancellationToken ct)
    {
        db.EmailSettings.Update(settings);
        await db.SaveChangesAsync(ct);
    }
}
