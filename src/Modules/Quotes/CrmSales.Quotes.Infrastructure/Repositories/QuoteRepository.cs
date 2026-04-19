using CrmSales.Quotes.Domain.Entities;
using CrmSales.Quotes.Domain.Repositories;
using CrmSales.Quotes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Quotes.Infrastructure.Repositories;

internal sealed class QuoteRepository(QuotesDbContext dbContext) : IQuoteRepository
{
    public async Task<Quote?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Quotes.Include(q => q.LineItems)
            .FirstOrDefaultAsync(q => q.Id == id, ct);

    public async Task<IReadOnlyList<Quote>> GetAllAsync(CancellationToken ct = default) =>
        await dbContext.Quotes.Include(q => q.LineItems)
            .OrderByDescending(q => q.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<Quote>> GetByOpportunityAsync(Guid opportunityId, CancellationToken ct = default) =>
        await dbContext.Quotes.Include(q => q.LineItems)
            .Where(q => q.OpportunityId == opportunityId).ToListAsync(ct);

    public async Task<IReadOnlyList<Quote>> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default) =>
        await dbContext.Quotes.Where(q => q.OwnerId == ownerId)
            .OrderByDescending(q => q.CreatedAt).ToListAsync(ct);

    public async Task<Quote?> GetByNumberAsync(string quoteNumber, CancellationToken ct = default) =>
        await dbContext.Quotes.Include(q => q.LineItems)
            .FirstOrDefaultAsync(q => q.QuoteNumber == quoteNumber, ct);

    public async Task AddAsync(Quote aggregate, CancellationToken ct = default)
    {
        await dbContext.Quotes.AddAsync(aggregate, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Quote aggregate, CancellationToken ct = default)
    {
        dbContext.Quotes.Update(aggregate);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Quote aggregate, CancellationToken ct = default)
    {
        dbContext.Quotes.Remove(aggregate);
        await dbContext.SaveChangesAsync(ct);
    }
}
