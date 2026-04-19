using CrmSales.Products.Domain.Entities;
using CrmSales.Products.Domain.Repositories;
using CrmSales.SharedKernel;

namespace CrmSales.Products.Application.Products.Commands.CreateProduct;

/// <summary>
/// Wolverine discovers this handler by the Handle method naming convention.
/// Dependencies are injected directly into the method parameters.
/// </summary>
public static class CreateProductHandler
{
    public static async Task<Result<Guid>> Handle(
        CreateProductCommand command,
        IProductRepository productRepository,
        IProductCategoryRepository categoryRepository,
        CancellationToken ct)
    {
        if (!await categoryRepository.ExistsAsync(command.CategoryId, ct))
            return Result.Failure<Guid>(new Error("Product.CategoryNotFound",
                $"Category '{command.CategoryId}' not found."));

        if (!await productRepository.IsSkuUniqueAsync(command.Sku, null, ct))
            return Result.Failure<Guid>(new Error("Product.DuplicateSku",
                $"SKU '{command.Sku}' is already in use."));

        var product = Product.Create(
            command.Name, command.Description, command.Sku,
            command.Price, command.Currency, command.CategoryId, command.StockQuantity);

        await productRepository.AddAsync(product, ct);
        return Result.Success(product.Id);
    }
}
