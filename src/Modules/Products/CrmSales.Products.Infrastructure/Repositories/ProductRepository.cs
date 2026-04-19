using CrmSales.Products.Domain.Entities;
using CrmSales.Products.Domain.Repositories;
using CrmSales.Products.Infrastructure.Persistence;
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

    public async Task<IReadOnlyList<Product>> SearchAsync(string? term, bool? isActive, CancellationToken ct = default)
    {
        var query = dbContext.Products.AsQueryable();
        if (!string.IsNullOrWhiteSpace(term))
            query = query.Where(p => p.Name.Contains(term) || p.Sku.Value.Contains(term));
        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);
        return await query.OrderBy(p => p.Name).ToListAsync(ct);
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
