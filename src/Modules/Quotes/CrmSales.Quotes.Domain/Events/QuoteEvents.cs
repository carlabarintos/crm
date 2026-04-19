using CrmSales.SharedKernel.Domain;

namespace CrmSales.Quotes.Domain.Events;

public sealed record QuoteCreatedEvent(
    Guid QuoteId, string QuoteNumber, Guid OpportunityId, Guid OwnerId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record QuoteSentEvent(
    Guid QuoteId, string QuoteNumber, Guid OpportunityId,
    decimal TotalAmount, string Currency) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record QuoteAcceptedEvent(
    Guid QuoteId, string QuoteNumber, Guid OpportunityId,
    decimal TotalAmount, string Currency, Guid OwnerId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
