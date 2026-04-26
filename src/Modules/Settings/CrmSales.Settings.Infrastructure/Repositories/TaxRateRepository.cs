using CrmSales.Settings.Domain.Entities;
using CrmSales.Settings.Domain.Repositories;
using CrmSales.Settings.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Settings.Infrastructure.Repositories;

internal sealed class TaxRateRepository(SettingsDbContext dbContext) : ITaxRateRepository
{
    public async Task<TaxRate?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.TaxRates.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<TaxRate>> GetAllAsync(CancellationToken ct = default) =>
        await dbContext.TaxRates.ToListAsync(ct);

    public async Task<IReadOnlyList<TaxRate>> GetActiveAsync(CancellationToken ct = default) =>
        await dbContext.TaxRates.Where(t => t.IsActive).ToListAsync(ct);

    public async Task<TaxRate?> GetDefaultAsync(CancellationToken ct = default) =>
        await dbContext.TaxRates.FirstOrDefaultAsync(t => t.IsDefault, ct);

    public async Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default) =>
        await dbContext.TaxRates
            .Where(t => t.Name == name && (excludeId == null || t.Id != excludeId))
            .AnyAsync(ct);

    public async Task AddAsync(TaxRate aggregate, CancellationToken ct = default)
    {
        await dbContext.TaxRates.AddAsync(aggregate, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(TaxRate aggregate, CancellationToken ct = default)
    {
        dbContext.TaxRates.Update(aggregate);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(TaxRate aggregate, CancellationToken ct = default)
    {
        dbContext.TaxRates.Remove(aggregate);
        await dbContext.SaveChangesAsync(ct);
    }
}
