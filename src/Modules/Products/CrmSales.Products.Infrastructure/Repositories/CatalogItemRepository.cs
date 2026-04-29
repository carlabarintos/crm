using CrmSales.Products.Domain.Entities;
using CrmSales.Products.Domain.Repositories;
using CrmSales.Products.Infrastructure.Persistence;
using CrmSales.SharedKernel.Application;
using CrmSales.SharedKernel.Catalog;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Products.Infrastructure.Repositories;

internal sealed class CatalogItemRepository(ProductsDbContext dbContext) : ICatalogItemRepository
{
    public async Task<CatalogItem?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.CatalogItems.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<CursorPaginationResult<CatalogItemLookup>> SearchAsync(
        string? term, bool? isActive, CatalogItemType? type, int limit, string? cursor, CancellationToken ct = default)
    {
        var query = dbContext.CatalogItems.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var pattern = $"%{term}%";
            query = query.Where(c => EF.Functions.ILike(c.Name, pattern));
        }
        if (isActive.HasValue)
            query = query.Where(c => c.IsActive == isActive.Value);
        if (type.HasValue)
        {
            var typeString = type.Value.ToString();
            query = query.Where(c => EF.Property<string>(c, "Discriminator") == typeString);
        }
        if (!string.IsNullOrEmpty(cursor) && Guid.TryParse(cursor, out var cursorId))
            query = query.Where(c => c.Id > cursorId);

        var items = await query
            .OrderBy(c => c.Id)
            .Take(limit + 1)
            .Select(c => new
            {
                c.Id, c.Name, c.Price, c.IsActive,
                TypeStr = EF.Property<string>(c, "Discriminator")
            })
            .ToListAsync(ct);

        string? nextCursor = null;
        if (items.Count > limit)
        {
            items.RemoveAt(items.Count - 1);
            nextCursor = items[^1].Id.ToString();
        }

        var lookups = items.Select(x => new CatalogItemLookup(
            x.Id, x.Name, x.Price.Amount, x.Price.Currency,
            Enum.Parse<CatalogItemType>(x.TypeStr), x.IsActive)).ToList();

        return CursorPaginationResult<CatalogItemLookup>.Create(lookups, nextCursor);
    }
}
