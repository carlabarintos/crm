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
            .RequireAuthorization();

        group.MapGet("/", async (
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? search = null,
            [FromQuery] string? eventType = null,
            [FromQuery] string? entityType = null,
            [FromQuery] string? actor = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            ITenantContext tenant = default!,
            MasterDbContext db = default!,
            CancellationToken ct = default) =>
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(1, page);

            var query = db.AuditLogs
                .Where(a => a.TenantId == tenant.TenantId);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(a => EF.Functions.ILike(a.Description, $"%{search}%") ||
                                         EF.Functions.ILike(a.Actor, $"%{search}%"));

            if (!string.IsNullOrWhiteSpace(eventType))
                query = query.Where(a => a.EventType == eventType);

            if (!string.IsNullOrWhiteSpace(entityType))
                query = query.Where(a => a.EntityType == entityType);

            if (!string.IsNullOrWhiteSpace(actor))
                query = query.Where(a => a.Actor == actor);

            if (from.HasValue)
                query = query.Where(a => a.OccurredAt >= from.Value);

            if (to.HasValue)
                query = query.Where(a => a.OccurredAt <= to.Value);

            query = query.OrderByDescending(a => a.OccurredAt);

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

        group.MapGet("/summary", async (
            ITenantContext tenant = default!,
            MasterDbContext db = default!,
            CancellationToken ct = default) =>
        {
            var todayUtc = DateTime.UtcNow.Date;
            var weekUtc = todayUtc.AddDays(-6);

            var logs = db.AuditLogs.Where(a => a.TenantId == tenant.TenantId);

            var totalAll = await logs.CountAsync(ct);
            var totalToday = await logs.CountAsync(a => a.OccurredAt >= todayUtc, ct);
            var totalWeek = await logs.CountAsync(a => a.OccurredAt >= weekUtc, ct);

            var topActor = await logs
                .GroupBy(a => a.Actor)
                .OrderByDescending(g => g.Count())
                .Select(g => new { Actor = g.Key, Count = g.Count() })
                .FirstOrDefaultAsync(ct);

            var topEvent = await logs
                .GroupBy(a => a.EventType)
                .OrderByDescending(g => g.Count())
                .Select(g => new { EventType = g.Key, Count = g.Count() })
                .FirstOrDefaultAsync(ct);

            var actors = await logs
                .Select(a => a.Actor)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync(ct);

            var eventTypes = await logs
                .Select(a => a.EventType)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync(ct);

            return Results.Ok(new
            {
                TotalAll = totalAll,
                TotalToday = totalToday,
                TotalWeek = totalWeek,
                TopActor = topActor?.Actor,
                TopActorCount = topActor?.Count ?? 0,
                TopEvent = topEvent?.EventType,
                TopEventCount = topEvent?.Count ?? 0,
                Actors = actors,
                EventTypes = eventTypes
            });
        });

        return app;
    }
}
