namespace CrmSales.Api.Auditing;

public sealed class AuditLog
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string TenantId { get; init; } = "";
    public string EventType { get; init; } = "";
    public string EntityType { get; init; } = "";
    public string EntityId { get; init; } = "";
    public string Description { get; init; } = "";
    public string Actor { get; init; } = "";
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
