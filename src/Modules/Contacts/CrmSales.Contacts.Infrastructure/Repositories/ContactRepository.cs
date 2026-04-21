using CrmSales.Contacts.Domain.Entities;
using CrmSales.Contacts.Domain.Repositories;
using CrmSales.Contacts.Infrastructure.Persistence;
using CrmSales.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Contacts.Infrastructure.Repositories;

internal sealed class ContactRepository(ContactsDbContext dbContext) : IContactRepository
{
    public async Task<Contact?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Contacts.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Contact>> GetAllAsync(CancellationToken ct = default) =>
        await dbContext.Contacts.OrderBy(c => c.LastName).ThenBy(c => c.FirstName).ToListAsync(ct);

    public async Task<IReadOnlyList<Contact>> SearchAsync(string? search, CancellationToken ct = default)
    {
        var query = dbContext.Contacts.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(c =>
                EF.Functions.ILike(c.FirstName, pattern) ||
                EF.Functions.ILike(c.LastName, pattern) ||
                (c.Email != null && EF.Functions.ILike(c.Email, pattern)) ||
                (c.Company != null && EF.Functions.ILike(c.Company, pattern)));
        }
        return await query.OrderBy(c => c.LastName).ThenBy(c => c.FirstName).ToListAsync(ct);
    }

    public async Task<CursorPaginationResult<Contact>> SearchPagedAsync(
        string? search, int limit, string? cursor, CancellationToken ct = default)
    {
        var query = dbContext.Contacts.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(c =>
                EF.Functions.ILike(c.FirstName, pattern) ||
                EF.Functions.ILike(c.LastName, pattern) ||
                (c.Email != null && EF.Functions.ILike(c.Email, pattern)) ||
                (c.Company != null && EF.Functions.ILike(c.Company, pattern)));
        }

        if (!string.IsNullOrEmpty(cursor) && Guid.TryParse(cursor, out var cursorId))
            query = query.Where(c => c.Id > cursorId);

        var items = await query
            .OrderBy(c => c.Id)
            .Take(limit + 1)
            .ToListAsync(ct);

        string? nextCursor = null;
        if (items.Count > limit)
        {
            items.RemoveAt(items.Count - 1);
            nextCursor = items[^1].Id.ToString();
        }

        return CursorPaginationResult<Contact>.Create(items, nextCursor);
    }

    public async Task<ContactSummaryData> GetSummaryAsync(CancellationToken ct = default)
    {
        var stats = await dbContext.Contacts
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Count(), Active = g.Count(c => c.IsActive) })
            .FirstOrDefaultAsync(ct);
        return new ContactSummaryData(stats?.Total ?? 0, stats?.Active ?? 0);
    }

    public async Task AddAsync(Contact aggregate, CancellationToken ct = default)
    {
        await dbContext.Contacts.AddAsync(aggregate, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Contact aggregate, CancellationToken ct = default)
    {
        dbContext.Contacts.Update(aggregate);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Contact aggregate, CancellationToken ct = default)
    {
        dbContext.Contacts.Remove(aggregate);
        await dbContext.SaveChangesAsync(ct);
    }
}
