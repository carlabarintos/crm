using CrmSales.Opportunities.Domain.Entities;
using CrmSales.Opportunities.Domain.Repositories;
using CrmSales.Opportunities.Infrastructure.Persistence;
using CrmSales.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Opportunities.Infrastructure.Repositories;

internal sealed class OpportunityRepository(OpportunitiesDbContext dbContext) : IOpportunityRepository
{
    public async Task<Opportunity?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Opportunities.Include(o => o.Activities)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<IReadOnlyList<Opportunity>> GetAllAsync(CancellationToken ct = default) =>
        await dbContext.Opportunities.OrderByDescending(o => o.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<Opportunity>> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default) =>
        await dbContext.Opportunities.Where(o => o.OwnerId == ownerId)
            .OrderByDescending(o => o.UpdatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<Opportunity>> GetByStageAsync(OpportunityStage stage, CancellationToken ct = default) =>
        await dbContext.Opportunities.Where(o => o.Stage == stage).ToListAsync(ct);

    public async Task<CursorPaginationResult<Opportunity>> SearchAsync(
        string? term, OpportunityStage? stage, Guid? ownerId, int limit, string? cursor, CancellationToken ct = default)
    {
        var query = dbContext.Opportunities.AsQueryable();
        if (!string.IsNullOrWhiteSpace(term))
        {
            var pattern = $"%{term}%";
            query = query.Where(o => EF.Functions.ILike(o.Name, pattern) || EF.Functions.ILike(o.AccountName, pattern));
        }
        if (stage.HasValue) query = query.Where(o => o.Stage == stage.Value);
        if (ownerId.HasValue) query = query.Where(o => o.OwnerId == ownerId.Value);

        if (!string.IsNullOrEmpty(cursor) && Guid.TryParse(cursor, out var cursorId))
            query = query.Where(o => o.Id > cursorId);

        var items = await query
            .OrderBy(o => o.Id)
            .Take(limit + 1)
            .ToListAsync(ct);

        string? nextCursor = null;
        if (items.Count > limit)
        {
            items.RemoveAt(items.Count - 1);
            nextCursor = items[^1].Id.ToString();
        }

        return CursorPaginationResult<Opportunity>.Create(items, nextCursor);
    }

    public async Task<OpportunitySummaryData> GetSummaryAsync(CancellationToken ct = default)
    {
        var byStage = await dbContext.Opportunities
            .GroupBy(o => o.Stage)
            .Select(g => new { Stage = g.Key, Count = g.Count(), Value = g.Sum(o => o.EstimatedValue) })
            .ToListAsync(ct);

        var pipelineValue = await dbContext.Opportunities
            .Where(o => o.Stage != OpportunityStage.ClosedWon && o.Stage != OpportunityStage.ClosedLost)
            .SumAsync(o => o.EstimatedValue, ct);

        var weightedValue = await dbContext.Opportunities
            .Where(o => o.Stage != OpportunityStage.ClosedWon && o.Stage != OpportunityStage.ClosedLost)
            .SumAsync(o => o.EstimatedValue * o.Probability / 100m, ct);

        var currency = await dbContext.Opportunities
            .Select(o => o.Currency).FirstOrDefaultAsync(ct) ?? "USD";

        var stageSummaries = byStage
            .Select(s => new StageSummaryData(s.Stage.ToString(), s.Count, s.Value))
            .ToList();

        var total = byStage.Sum(s => s.Count);
        var won  = byStage.FirstOrDefault(s => s.Stage == OpportunityStage.ClosedWon)?.Count ?? 0;
        var lost = byStage.FirstOrDefault(s => s.Stage == OpportunityStage.ClosedLost)?.Count ?? 0;

        var wonDates = await dbContext.Opportunities
            .Where(o => o.Stage == OpportunityStage.ClosedWon)
            .Select(o => new { o.CreatedAt, o.UpdatedAt })
            .ToListAsync(ct);
        var avgDays = wonDates.Count > 0
            ? wonDates.Average(o => (o.UpdatedAt - o.CreatedAt).TotalDays)
            : 0.0;

        return new OpportunitySummaryData(total, won, lost, pipelineValue, weightedValue, currency, stageSummaries, avgDays);
    }

    public async Task<List<TopOpportunityData>> GetTopOpportunitiesAsync(int count, CancellationToken ct = default)
    {
        var items = await dbContext.Opportunities
            .OrderByDescending(o => o.EstimatedValue)
            .Take(count)
            .Select(o => new { o.Name, o.AccountName, o.Stage, o.EstimatedValue, o.Currency })
            .ToListAsync(ct);

        return items.Select(o => new TopOpportunityData(
            o.Name, o.AccountName, o.Stage.ToString(), o.EstimatedValue, o.Currency)).ToList();
    }

    public async Task AddAsync(Opportunity aggregate, CancellationToken ct = default)
    {
        await dbContext.Opportunities.AddAsync(aggregate, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Opportunity aggregate, CancellationToken ct = default)
    {
        dbContext.Opportunities.Update(aggregate);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Opportunity aggregate, CancellationToken ct = default)
    {
        dbContext.Opportunities.Remove(aggregate);
        await dbContext.SaveChangesAsync(ct);
    }
}
