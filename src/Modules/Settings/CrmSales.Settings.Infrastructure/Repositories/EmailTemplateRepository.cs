using CrmSales.Settings.Domain.Entities;
using CrmSales.Settings.Domain.Enums;
using CrmSales.Settings.Domain.Repositories;
using CrmSales.Settings.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Settings.Infrastructure.Repositories;

internal sealed class EmailTemplateRepository(SettingsDbContext db) : IEmailTemplateRepository
{
    public Task<List<EmailTemplate>> GetAllAsync(CancellationToken ct)
        => db.EmailTemplates.OrderBy(t => t.TemplateType).ToListAsync(ct);

    public Task<EmailTemplate?> GetByTypeAsync(EmailTemplateType type, CancellationToken ct)
        => db.EmailTemplates.FirstOrDefaultAsync(t => t.TemplateType == type, ct);

    public async Task AddAsync(EmailTemplate template, CancellationToken ct)
    {
        db.EmailTemplates.Add(template);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(EmailTemplate template, CancellationToken ct)
    {
        db.EmailTemplates.Update(template);
        await db.SaveChangesAsync(ct);
    }
}
