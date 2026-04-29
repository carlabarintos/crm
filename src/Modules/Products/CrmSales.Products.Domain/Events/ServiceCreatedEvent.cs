using CrmSales.SharedKernel.Domain;

namespace CrmSales.Products.Domain.Events;

public sealed record ServiceCreatedEvent(
    Guid ServiceId,
    string Name,
    string ServiceCode,
    decimal Price,
    string Currency) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
