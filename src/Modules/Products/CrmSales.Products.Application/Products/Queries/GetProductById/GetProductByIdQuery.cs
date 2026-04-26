using CrmSales.Products.Application.Products.DTOs;
using CrmSales.Products.Domain.Repositories;
using CrmSales.SharedKernel;
using CrmSales.SharedKernel.Application;

namespace CrmSales.Products.Application.Products.Queries.GetProductById;

public record GetProductByIdQuery(Guid Id) : IQuery<ProductDto>;

public static class GetProductByIdHandler
{
    public static async Task<Result<ProductDto>> Handle(
        GetProductByIdQuery query,
        IProductRepository productRepository,
        CancellationToken ct)
    {
        var product = await productRepository.GetByIdAsync(query.Id, ct);
        if (product is null)
            return Result.Failure<ProductDto>(Error.NotFoundFor("Product", query.Id));

        return Result.Success(new ProductDto(
            product.Id, product.Name, product.Description,
            product.Sku.Value, product.Price.Amount, product.Price.Currency,
            product.CategoryId, null, product.IsActive, product.StockQuantity, product.ReorderPoint,
            product.CreatedAt, product.UpdatedAt));
    }
}
