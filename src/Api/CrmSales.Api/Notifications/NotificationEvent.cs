namespace CrmSales.Api.Notifications;

public sealed record NotificationEvent(
    string Type,
    string Title,
    string Message,
    string EntityId,
    string Actor,
    string TenantId,
    DateTime OccurredAt);
