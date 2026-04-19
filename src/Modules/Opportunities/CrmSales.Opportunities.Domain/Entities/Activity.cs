using CrmSales.SharedKernel.Domain;

namespace CrmSales.Opportunities.Domain.Entities;

public sealed class Activity : Entity<Guid>
{
    public Guid OpportunityId { get; private set; }
    public string Type { get; private set; }
    public string Notes { get; private set; }
    public Guid PerformedById { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private Activity() { Type = string.Empty; Notes = string.Empty; }

    internal static Activity Create(Guid opportunityId, string type, string notes, Guid performedById) =>
        new()
        {
            Id = Guid.NewGuid(),
            OpportunityId = opportunityId,
            Type = type.Trim(),
            Notes = notes.Trim(),
            PerformedById = performedById,
            OccurredAt = DateTime.UtcNow
        };
}
