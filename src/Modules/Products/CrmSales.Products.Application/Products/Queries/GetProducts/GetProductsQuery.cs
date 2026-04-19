using CrmSales.Products.Application.Products.DTOs;
using CrmSales.Products.Domain.Repositories;
using CrmSales.SharedKernel;
using CrmSales.SharedKernel.Application;

namespace CrmSales.Products.Application.Products.Queries.GetProducts;

public record GetProductsQuery(string? SearchTerm = null, bool? IsActive = null)
    : IQuery<IReadOnlyList<ProductDto>>;

public static class GetProductsHandler
{
    public static async Task<Result<IReadOnlyList<ProductDto>>> Handle(
        GetProductsQuery query,
        IProductRepository productRepository,
        CancellationToken ct)
    {
        var products = await productRepository.SearchAsync(query.SearchTerm, query.IsActive, ct);

        IReadOnlyList<ProductDto> dtos = products.Select(p => new ProductDto(
            p.Id, p.Name, p.Description,
            p.Sku.Value, p.Price.Amount, p.Price.Currency,
            p.CategoryId, null, p.IsActive, p.StockQuantity,
            p.CreatedAt, p.UpdatedAt)).ToList();

        return Result.Success(dtos);
    }
}
