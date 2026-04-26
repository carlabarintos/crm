using CrmSales.Opportunities.Domain.Entities;
using CrmSales.SharedKernel.Application;
using CrmSales.SharedKernel.Domain;

namespace CrmSales.Opportunities.Domain.Repositories;

public record StageSummaryData(string Stage, int Count, decimal Value);
public record OpportunitySummaryData(
    int Total, int Won, int Lost,
    decimal PipelineValue, decimal WeightedValue,
    string Currency,
    List<StageSummaryData> ByStage,
    double AvgDaysToClose);

public record TopOpportunityData(string Name, string Account, string Stage, decimal Value, string Currency);

public interface IOpportunityRepository : IRepository<Opportunity, Guid>
{
    Task<IReadOnlyList<Opportunity>> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default);
    Task<IReadOnlyList<Opportunity>> GetByStageAsync(OpportunityStage stage, CancellationToken ct = default);
    Task<CursorPaginationResult<Opportunity>> SearchAsync(string? term, OpportunityStage? stage, Guid? ownerId, int limit, string? cursor, CancellationToken ct = default);
    Task<OpportunitySummaryData> GetSummaryAsync(CancellationToken ct = default);
    Task<List<TopOpportunityData>> GetTopOpportunitiesAsync(int count, CancellationToken ct = default);
    Task<List<Opportunity>> GetExpiringSoonAsync(int days, int limit, Guid? ownerId = null, CancellationToken ct = default);
}
