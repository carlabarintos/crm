using CrmSales.Opportunities.Domain.Entities;
using CrmSales.Opportunities.Domain.Repositories;
using CrmSales.Opportunities.Infrastructure.Persistence;
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

    public async Task<IReadOnlyList<Opportunity>> SearchAsync(
        string? term, OpportunityStage? stage, Guid? ownerId, CancellationToken ct = default)
    {
        var query = dbContext.Opportunities.AsQueryable();
        if (!string.IsNullOrWhiteSpace(term))
            query = query.Where(o => o.Name.Contains(term) || o.AccountName.Contains(term));
        if (stage.HasValue) query = query.Where(o => o.Stage == stage.Value);
        if (ownerId.HasValue) query = query.Where(o => o.OwnerId == ownerId.Value);
        return await query.OrderByDescending(o => o.UpdatedAt).ToListAsync(ct);
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
