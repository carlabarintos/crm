using CrmSales.Products.Application.Products.DTOs;
using CrmSales.Products.Domain.Repositories;
using CrmSales.SharedKernel;
using CrmSales.SharedKernel.Application;

namespace CrmSales.Products.Application.Products.Queries.GetProducts;

public record GetProductsQuery(string? SearchTerm = null, bool? IsActive = null, int Limit = 20, string? Cursor = null)
    : IQuery<CursorPaginationResult<ProductDto>>;

public static class GetProductsHandler
{
    public static async Task<Result<CursorPaginationResult<ProductDto>>> Handle(
        GetProductsQuery query,
        IProductRepository productRepository,
        CancellationToken ct)
    {
        var result = await productRepository.SearchAsync(query.SearchTerm, query.IsActive, query.Limit, query.Cursor, ct);

        var dtos = result.Items.Select(p => new ProductDto(
            p.Id, p.Name, p.Description,
            p.Sku.Value, p.Price.Amount, p.Price.Currency,
            p.CategoryId, null, p.IsActive, p.StockQuantity,
            p.CreatedAt, p.UpdatedAt)).ToList();

        var paginationResult = CursorPaginationResult<ProductDto>.Create(dtos, result.NextCursor);
        return Result.Success(paginationResult);
    }
}
