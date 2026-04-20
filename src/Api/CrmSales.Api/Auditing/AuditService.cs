using CrmSales.Api.Master;

namespace CrmSales.Api.Auditing;

public sealed class AuditService(MasterDbContext db) : IAuditService
{
    public async Task LogAsync(string tenantId, string eventType, string entityType,
        string entityId, string description, string actor, CancellationToken ct = default)
    {
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            EventType = eventType,
            EntityType = entityType,
            EntityId = entityId,
            Description = description,
            Actor = actor
        });
        await db.SaveChangesAsync(ct);
    }
}
