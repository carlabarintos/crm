using CrmSales.Quotes.Domain.Entities;
using CrmSales.Quotes.Domain.Repositories;
using CrmSales.Quotes.Infrastructure.Persistence;
using CrmSales.SharedKernel.Application;
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

    public async Task<CursorPaginationResult<Quote>> SearchPagedAsync(
        string? search, string? status, Guid? opportunityId, int limit, string? cursor, CancellationToken ct = default)
    {
        var query = dbContext.Quotes.Include(q => q.LineItems).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(q => EF.Functions.ILike(q.QuoteNumber, $"%{search}%"));

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<QuoteStatus>(status, out var statusEnum))
            query = query.Where(q => q.Status == statusEnum);

        if (opportunityId.HasValue)
            query = query.Where(q => q.OpportunityId == opportunityId.Value);

        if (!string.IsNullOrEmpty(cursor) && Guid.TryParse(cursor, out var cursorId))
            query = query.Where(q => q.Id > cursorId);

        var items = await query
            .OrderBy(q => q.Id)
            .Take(limit + 1)
            .ToListAsync(ct);

        string? nextCursor = null;
        if (items.Count > limit)
        {
            items.RemoveAt(items.Count - 1);
            nextCursor = items[^1].Id.ToString();
        }

        return CursorPaginationResult<Quote>.Create(items, nextCursor);
    }

    public async Task<QuoteSummaryData> GetSummaryAsync(CancellationToken ct = default)
    {
        var counts = await dbContext.Quotes
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total    = g.Count(),
                Draft    = g.Count(q => q.Status == QuoteStatus.Draft),
                Sent     = g.Count(q => q.Status == QuoteStatus.Sent),
                Accepted = g.Count(q => q.Status == QuoteStatus.Accepted),
                Rejected = g.Count(q => q.Status == QuoteStatus.Rejected),
                Expired  = g.Count(q => q.Status == QuoteStatus.Expired),
            })
            .FirstOrDefaultAsync(ct);

        var sentValue = await dbContext.Quotes
            .Where(q => q.Status == QuoteStatus.Sent)
            .Join(dbContext.LineItems, q => q.Id, l => l.QuoteId,
                (_, l) => l.UnitPrice * l.Quantity * (1 - l.DiscountPercent / 100m))
            .SumAsync(ct);

        var acceptedValue = await dbContext.Quotes
            .Where(q => q.Status == QuoteStatus.Accepted)
            .Join(dbContext.LineItems, q => q.Id, l => l.QuoteId,
                (_, l) => l.UnitPrice * l.Quantity * (1 - l.DiscountPercent / 100m))
            .SumAsync(ct);

        var currency = await dbContext.Quotes
            .Select(q => q.Currency).FirstOrDefaultAsync(ct) ?? "USD";

        return new QuoteSummaryData(
            counts?.Total ?? 0, counts?.Draft ?? 0, counts?.Sent ?? 0,
            counts?.Accepted ?? 0, counts?.Rejected ?? 0, counts?.Expired ?? 0,
            sentValue, acceptedValue, currency);
    }

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
