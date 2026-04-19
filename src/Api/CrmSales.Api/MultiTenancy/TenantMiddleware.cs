using CrmSales.SharedKernel.MultiTenancy;

namespace CrmSales.Api.MultiTenancy;

public class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        //var companyId = context.User.FindFirst("company_id")?.Value;
        //if (!string.IsNullOrWhiteSpace(companyId))
        //    tenantContext.TenantId = companyId;

        await next(context);
    }
}
