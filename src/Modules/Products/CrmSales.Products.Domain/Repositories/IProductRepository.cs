using CrmSales.Products.Domain.Entities;
using CrmSales.SharedKernel.Application;
using CrmSales.SharedKernel.Domain;

namespace CrmSales.Products.Domain.Repositories;

public record ProductSummaryData(int Total, int Active, int LowStock, int OutOfStock, decimal InventoryValue, string Currency);

public interface IProductRepository : IRepository<Product, Guid>
{
    Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetByCategoryAsync(Guid categoryId, CancellationToken ct = default);
    Task<bool> IsSkuUniqueAsync(string sku, Guid? excludeProductId = null, CancellationToken ct = default);
    Task<CursorPaginationResult<Product>> SearchAsync(string? term, bool? isActive, bool lowInventory, int limit, string? cursor, CancellationToken ct = default);
    Task<ProductSummaryData> GetSummaryAsync(CancellationToken ct = default);
}

public interface IProductCategoryRepository : IRepository<ProductCategory, Guid>
{
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task<CursorPaginationResult<ProductCategory>> SearchAsync(string? term, int limit, string? cursor, CancellationToken ct = default);
}
