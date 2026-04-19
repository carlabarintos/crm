using CrmSales.Opportunities.Domain.Entities;
using CrmSales.SharedKernel.Domain;

namespace CrmSales.Opportunities.Domain.Repositories;

public interface IOpportunityRepository : IRepository<Opportunity, Guid>
{
    Task<IReadOnlyList<Opportunity>> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default);
    Task<IReadOnlyList<Opportunity>> GetByStageAsync(OpportunityStage stage, CancellationToken ct = default);
    Task<IReadOnlyList<Opportunity>> SearchAsync(string? term, OpportunityStage? stage, Guid? ownerId, CancellationToken ct = default);
}
