using CrmSales.Api.Auditing;
using CrmSales.Api.Master;
using CrmSales.SharedKernel.MultiTenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Api.Endpoints;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit")
            .WithTags("Audit")
            .RequireAuthorization(p => p.RequireRole("Admin", "SalesManager"));

        group.MapGet("/", async (
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            ITenantContext tenant = default!,
            MasterDbContext db = default!,
            CancellationToken ct = default) =>
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(1, page);

            var query = db.AuditLogs
                .Where(a => a.TenantId == tenant.TenantId)
                .OrderByDescending(a => a.OccurredAt);

            var total = await query.CountAsync(ct);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Id, a.EventType, a.EntityType, a.EntityId,
                    a.Description, a.Actor, a.OccurredAt
                })
                .ToListAsync(ct);

            return Results.Ok(new { Total = total, Page = page, PageSize = pageSize, Items = items });
        });

        return app;
    }
}
