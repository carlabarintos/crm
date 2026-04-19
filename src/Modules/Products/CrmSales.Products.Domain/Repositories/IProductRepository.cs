using CrmSales.Products.Domain.Entities;
using CrmSales.SharedKernel.Domain;

namespace CrmSales.Products.Domain.Repositories;

public interface IProductRepository : IRepository<Product, Guid>
{
    Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetByCategoryAsync(Guid categoryId, CancellationToken ct = default);
    Task<bool> IsSkuUniqueAsync(string sku, Guid? excludeProductId = null, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> SearchAsync(string? term, bool? isActive, CancellationToken ct = default);
}

public interface IProductCategoryRepository : IRepository<ProductCategory, Guid>
{
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
}
