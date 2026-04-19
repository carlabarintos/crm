using CrmSales.SharedKernel.Domain;
using CrmSales.Users.Domain.Entities;

namespace CrmSales.Users.Domain.Events;

public sealed record UserCreatedEvent(
    Guid UserId, string Email, string FullName, UserRole Role) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record UserDeactivatedEvent(Guid UserId, string Email) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
