using CrmSales.SharedKernel.MultiTenancy;

namespace CrmSales.Api.MultiTenancy;

public class TenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    private string? _override;

    public string TenantId
    {
        get
        {
            if (_override is not null) return _override;
            var companyId = accessor.HttpContext?.User.FindFirst("company_id")?.Value;
            return string.IsNullOrWhiteSpace(companyId) ? "master" : companyId;
        }
        set => _override = value;
    }
}
