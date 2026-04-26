using CrmSales.SharedKernel;
using CrmSales.SharedKernel.Application;

namespace CrmSales.Products.Application.Products.Commands.CreateProduct;

public record CreateProductCommand(
    string Name,
    string? Description,
    string Sku,
    decimal Price,
    string Currency,
    Guid CategoryId,
    int StockQuantity = 0,
    int ReorderPoint = 10) : ICommand<Guid>;
