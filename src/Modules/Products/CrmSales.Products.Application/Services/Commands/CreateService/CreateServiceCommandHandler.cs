using CrmSales.Products.Domain.Entities;
using CrmSales.Products.Domain.Repositories;
using CrmSales.SharedKernel;

namespace CrmSales.Products.Application.Services.Commands.CreateService;

public static class CreateServiceHandler
{
    public static async Task<Result<Guid>> Handle(
        CreateServiceCommand command,
        IServiceRepository serviceRepository,
        IProductCategoryRepository categoryRepository,
        CancellationToken ct)
    {
        if (!await categoryRepository.ExistsAsync(command.CategoryId, ct))
            return Result.Failure<Guid>(new Error("Service.CategoryNotFound",
                $"Category '{command.CategoryId}' not found."));

        if (!await serviceRepository.IsServiceCodeUniqueAsync(command.ServiceCode, null, ct))
            return Result.Failure<Guid>(new Error("Service.DuplicateCode",
                $"Service code '{command.ServiceCode}' is already in use."));

        var service = Service.Create(
            command.Name, command.Description, command.ServiceCode,
            command.Price, command.Currency, command.CategoryId,
            command.UnitOfMeasure, command.EstimatedDurationMinutes);

        await serviceRepository.AddAsync(service, ct);
        return Result.Success(service.Id);
    }
}
