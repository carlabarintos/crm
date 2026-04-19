namespace CrmSales.SharedKernel.Domain;

/// <summary>
/// Marker for in-process domain events raised inside aggregates.
/// Wolverine discovers handlers via naming convention (Handle method).
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredOn { get; }
}
