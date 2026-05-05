using CrmSales.Api.Master;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Api.Middleware;

public sealed class SubscriptionEnforcementMiddleware(RequestDelegate next)
{
    private static readonly string[] ExemptPrefixes =
    [
        "/api/invoices", "/api/subscriptions", "/api/companies", "/api/billing",
        "/api/access-requests", "/api/notifications", "/scalar", "/health",
        "/openapi", "/authentication"
    ];

    private static readonly string[] ReadOnlyMethods = ["GET", "HEAD", "OPTIONS"];

    public async Task InvokeAsync(HttpContext context, MasterDbContext db)
    {
        if (IsExempt(context.Request.Path) || ReadOnlyMethods.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var companyIdClaim = context.User.FindFirst("company_id")?.Value;
        if (string.IsNullOrEmpty(companyIdClaim))
        {
            await next(context);
            return;
        }

        var company = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == companyIdClaim);

        if (company?.SubscriptionStatus is SubscriptionStatus.Expired or SubscriptionStatus.Suspended)
        {
            context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"error":"subscription_expired","message":"Your subscription has expired. Please contact your administrator to renew."}""");
            return;
        }

        await next(context);
    }

    private static bool IsExempt(PathString path)
    {
        foreach (var prefix in ExemptPrefixes)
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
