using CrmSales.Opportunities.Domain.Entities;
using CrmSales.SharedKernel.Domain;

namespace CrmSales.Opportunities.Domain.Events;

public sealed record OpportunityCreatedEvent(
    Guid OpportunityId, string Name, string AccountName, Guid OwnerId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record OpportunityStageChangedEvent(
    Guid OpportunityId, OpportunityStage OldStage, OpportunityStage NewStage) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record OpportunityWonEvent(
    Guid OpportunityId, string Name, string AccountName,
    decimal Value, string Currency) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
