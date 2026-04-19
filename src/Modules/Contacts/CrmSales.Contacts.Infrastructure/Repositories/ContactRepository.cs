using CrmSales.Contacts.Domain.Entities;
using CrmSales.Contacts.Domain.Repositories;
using CrmSales.Contacts.Infrastructure.Persistence;
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
            var lower = search.ToLowerInvariant();
            query = query.Where(c =>
                c.FirstName.ToLower().Contains(lower) ||
                c.LastName.ToLower().Contains(lower) ||
                (c.Email != null && c.Email.Contains(lower)) ||
                (c.Company != null && c.Company.ToLower().Contains(lower)));
        }
        return await query.OrderBy(c => c.LastName).ThenBy(c => c.FirstName).ToListAsync(ct);
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
