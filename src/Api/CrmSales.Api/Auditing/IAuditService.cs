namespace CrmSales.Api.Auditing;

public interface IAuditService
{
    Task LogAsync(string tenantId, string eventType, string entityType,
        string entityId, string description, string actor, CancellationToken ct = default);
}
