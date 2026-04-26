using CrmSales.Products.Domain.Repositories;
using CrmSales.SharedKernel;
using CrmSales.SharedKernel.Application;

namespace CrmSales.Products.Application.Products.Commands.UpdateProduct;

public record UpdateProductCommand(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    Guid CategoryId,
    int ReorderPoint = 10) : ICommand;

public static class UpdateProductHandler
{
    public static async Task<Result> Handle(
        UpdateProductCommand command,
        IProductRepository productRepository,
        CancellationToken ct)
    {
        var product = await productRepository.GetByIdAsync(command.Id, ct);
        if (product is null)
            return Result.Failure(Error.NotFoundFor("Product", command.Id));

        product.UpdateDetails(command.Name, command.Description, command.CategoryId);
        product.ChangePrice(command.Price, command.Currency);
        product.SetReorderPoint(command.ReorderPoint);
        await productRepository.UpdateAsync(product, ct);
        return Result.Success();
    }
}
