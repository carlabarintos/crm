using CrmSales.SharedKernel.Domain;

namespace CrmSales.Orders.Domain.Events;

public sealed record OrderCreatedEvent(
    Guid OrderId, string OrderNumber, Guid QuoteId,
    decimal TotalAmount, string Currency) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record OrderConfirmedEvent(
    Guid OrderId, string OrderNumber, Guid CustomerId,
    decimal TotalAmount, string Currency) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record OrderShippedEvent(
    Guid OrderId, string OrderNumber, string? TrackingInfo) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record OrderCancelledEvent(
    Guid OrderId, string OrderNumber, string Reason) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
