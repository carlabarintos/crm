using CrmSales.Settings.Domain.Entities;
using CrmSales.Settings.Domain.Enums;

namespace CrmSales.Settings.Domain.Repositories;

public interface IEmailTemplateRepository
{
    Task<List<EmailTemplate>> GetAllAsync(CancellationToken ct);
    Task<EmailTemplate?> GetByTypeAsync(EmailTemplateType type, CancellationToken ct);
    Task AddAsync(EmailTemplate template, CancellationToken ct);
    Task UpdateAsync(EmailTemplate template, CancellationToken ct);
}
