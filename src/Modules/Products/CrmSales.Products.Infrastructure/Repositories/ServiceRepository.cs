using CrmSales.Products.Domain.Entities;
using CrmSales.Products.Domain.Repositories;
using CrmSales.Products.Infrastructure.Persistence;
using CrmSales.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Products.Infrastructure.Repositories;

internal sealed class ServiceRepository(ProductsDbContext dbContext) : IServiceRepository
{
    public async Task<Service?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Services.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Service>> GetAllAsync(CancellationToken ct = default) =>
        await dbContext.Services.AsNoTracking().ToListAsync(ct);

    public async Task<Service?> GetByServiceCodeAsync(string serviceCode, CancellationToken ct = default) =>
        await dbContext.Services.FirstOrDefaultAsync(s => s.ServiceCode.Value == serviceCode.ToUpperInvariant(), ct);

    public async Task<bool> IsServiceCodeUniqueAsync(string serviceCode, Guid? excludeId = null, CancellationToken ct = default)
    {
        var normalized = serviceCode.ToUpperInvariant();
        return !await dbContext.Services
            .Where(s => s.ServiceCode.Value == normalized && (excludeId == null || s.Id != excludeId))
            .AnyAsync(ct);
    }

    public async Task<CursorPaginationResult<Service>> SearchAsync(string? term, bool? isActive, int limit, string? cursor, CancellationToken ct = default)
    {
        var query = dbContext.Services.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var pattern = $"%{term}%";
            query = query.Where(s => EF.Functions.ILike(s.Name, pattern) || EF.Functions.ILike(s.ServiceCode.Value, pattern));
        }
        if (isActive.HasValue)
            query = query.Where(s => s.IsActive == isActive.Value);
        if (!string.IsNullOrEmpty(cursor) && Guid.TryParse(cursor, out var cursorId))
            query = query.Where(s => s.Id > cursorId);

        var items = await query
            .OrderBy(s => s.Id)
            .Take(limit + 1)
            .ToListAsync(ct);

        string? nextCursor = null;
        if (items.Count > limit)
        {
            items.RemoveAt(items.Count - 1);
            nextCursor = items[^1].Id.ToString();
        }

        return CursorPaginationResult<Service>.Create(items, nextCursor);
    }

    public async Task AddAsync(Service aggregate, CancellationToken ct = default)
    {
        await dbContext.Services.AddAsync(aggregate, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Service aggregate, CancellationToken ct = default)
    {
        dbContext.Services.Update(aggregate);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Service aggregate, CancellationToken ct = default)
    {
        dbContext.Services.Remove(aggregate);
        await dbContext.SaveChangesAsync(ct);
    }

    public Task<int> CountAsync(CancellationToken ct = default) =>
        dbContext.Services.CountAsync(ct);
}
