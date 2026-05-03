using CrmSales.Api.Master;

namespace CrmSales.Api.Services;

public interface ICompanyLimitsService
{
    Task<CompanyLimits?> GetForCurrentTenantAsync(CancellationToken ct);
}
