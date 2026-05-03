using CrmSales.Api.Master;
using CrmSales.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Api.Services;

public sealed class CompanyLimitsService(MasterDbContext db, ITenantContext tenant) : ICompanyLimitsService
{
    public async Task<CompanyLimits?> GetForCurrentTenantAsync(CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        if (string.IsNullOrEmpty(tenantId)) return null;

        var company = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == tenantId, ct);

        if (company is null) return null;

        return await db.CompanyLimits
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.CompanyId == company.Id, ct);
    }
}
