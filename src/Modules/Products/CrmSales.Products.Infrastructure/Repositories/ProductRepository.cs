using CrmSales.Products.Domain.Entities;
using CrmSales.Products.Domain.Repositories;
using CrmSales.Products.Infrastructure.Persistence;
using CrmSales.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Products.Infrastructure.Repositories;

internal sealed class ProductRepository(ProductsDbContext dbContext) : IProductRepository
{
    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default) =>
        await dbContext.Products.ToListAsync(ct);

    public async Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default) =>
        await dbContext.Products.FirstOrDefaultAsync(p => p.Sku.Value == sku.ToUpperInvariant(), ct);

    public async Task<IReadOnlyList<Product>> GetByCategoryAsync(Guid categoryId, CancellationToken ct = default) =>
        await dbContext.Products.Where(p => p.CategoryId == categoryId).ToListAsync(ct);

    public async Task<bool> IsSkuUniqueAsync(string sku, Guid? excludeProductId = null, CancellationToken ct = default)
    {
        var normalized = sku.ToUpperInvariant();
        return !await dbContext.Products
            .Where(p => p.Sku.Value == normalized && (excludeProductId == null || p.Id != excludeProductId))
            .AnyAsync(ct);
    }

    public async Task<CursorPaginationResult<Product>> SearchAsync(string? term, bool? isActive, int limit, string? cursor, CancellationToken ct = default)
    {
        var query = dbContext.Products.AsQueryable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var pattern = $"%{term}%";
            query = query.Where(p => EF.Functions.ILike(p.Name, pattern) || EF.Functions.ILike(p.Sku.Value, pattern));
        }
        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        // Apply cursor if provided
        if (!string.IsNullOrEmpty(cursor) && Guid.TryParse(cursor, out var cursorId))
            query = query.Where(p => p.Id > cursorId);

        var items = await query
            .OrderBy(p => p.Id)
            .Take(limit + 1)
            .ToListAsync(ct);

        string? nextCursor = null;
        if (items.Count > limit)
        {
            items.RemoveAt(items.Count - 1);
            nextCursor = items[^1].Id.ToString();
        }

        return CursorPaginationResult<Product>.Create(items, nextCursor);
    }

    public async Task<ProductSummaryData> GetSummaryAsync(CancellationToken ct = default)
    {
        var stats = await dbContext.Products
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total      = g.Count(),
                Active     = g.Count(p => p.IsActive),
                LowStock   = g.Count(p => p.IsActive && p.StockQuantity > 0 && p.StockQuantity <= 10),
                OutOfStock = g.Count(p => p.IsActive && p.StockQuantity == 0),
            })
            .FirstOrDefaultAsync(ct);

        var inventoryValue = await dbContext.Products
            .Where(p => p.IsActive)
            .SumAsync(p => p.Price.Amount * p.StockQuantity, ct);

        var currency = await dbContext.Products
            .Where(p => p.IsActive)
            .Select(p => p.Price.Currency)
            .FirstOrDefaultAsync(ct) ?? "USD";

        return new ProductSummaryData(
            stats?.Total ?? 0, stats?.Active ?? 0,
            stats?.LowStock ?? 0, stats?.OutOfStock ?? 0,
            inventoryValue, currency);
    }

    public async Task AddAsync(Product aggregate, CancellationToken ct = default)
    {
        await dbContext.Products.AddAsync(aggregate, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Product aggregate, CancellationToken ct = default)
    {
        dbContext.Products.Update(aggregate);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Product aggregate, CancellationToken ct = default)
    {
        dbContext.Products.Remove(aggregate);
        await dbContext.SaveChangesAsync(ct);
    }
}

internal sealed class ProductCategoryRepository(ProductsDbContext dbContext) : IProductCategoryRepository
{
    public async Task<ProductCategory?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<ProductCategory>> GetAllAsync(CancellationToken ct = default) =>
        await dbContext.Categories.ToListAsync(ct);

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Categories.AnyAsync(c => c.Id == id, ct);

    public async Task<CursorPaginationResult<ProductCategory>> SearchAsync(string? term, int limit, string? cursor, CancellationToken ct = default)
    {
        var query = dbContext.Categories.AsQueryable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var pattern = $"%{term}%";
            query = query.Where(c => EF.Functions.ILike(c.Name, pattern) || EF.Functions.ILike(c.Description ?? "", pattern));
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

        return CursorPaginationResult<ProductCategory>.Create(items, nextCursor);
    }

    public async Task AddAsync(ProductCategory aggregate, CancellationToken ct = default)
    {
        await dbContext.Categories.AddAsync(aggregate, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ProductCategory aggregate, CancellationToken ct = default)
    {
        dbContext.Categories.Update(aggregate);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(ProductCategory aggregate, CancellationToken ct = default)
    {
        dbContext.Categories.Remove(aggregate);
        await dbContext.SaveChangesAsync(ct);
    }
}
