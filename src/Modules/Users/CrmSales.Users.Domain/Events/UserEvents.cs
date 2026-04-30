using CrmSales.SharedKernel.Domain;

namespace CrmSales.Users.Domain.Events;

public sealed record UserCreatedEvent(
    Guid UserId, string Email, string FullName) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record UserDeactivatedEvent(Guid UserId, string Email) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
