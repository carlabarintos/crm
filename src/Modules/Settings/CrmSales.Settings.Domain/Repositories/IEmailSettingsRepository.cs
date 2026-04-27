using CrmSales.Settings.Domain.Entities;

namespace CrmSales.Settings.Domain.Repositories;

public interface IEmailSettingsRepository
{
    Task<EmailSettings?> GetAsync(CancellationToken ct);
    Task AddAsync(EmailSettings settings, CancellationToken ct);
    Task UpdateAsync(EmailSettings settings, CancellationToken ct);
}
