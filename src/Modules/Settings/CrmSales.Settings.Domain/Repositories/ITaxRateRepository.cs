using CrmSales.Settings.Domain.Entities;
using CrmSales.SharedKernel.Domain;

namespace CrmSales.Settings.Domain.Repositories;

public interface ITaxRateRepository : IRepository<TaxRate, Guid>
{
    Task<IReadOnlyList<TaxRate>> GetActiveAsync(CancellationToken ct = default);
    Task<TaxRate?> GetDefaultAsync(CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default);
}
