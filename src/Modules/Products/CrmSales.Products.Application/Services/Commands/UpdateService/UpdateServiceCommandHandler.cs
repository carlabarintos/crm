using CrmSales.Products.Domain.Repositories;
using CrmSales.SharedKernel;

namespace CrmSales.Products.Application.Services.Commands.UpdateService;

public static class UpdateServiceHandler
{
    public static async Task<Result> Handle(
        UpdateServiceCommand command,
        IServiceRepository serviceRepository,
        CancellationToken ct)
    {
        var service = await serviceRepository.GetByIdAsync(command.Id, ct);
        if (service is null)
            return Result.Failure(new Error("Service.NotFound", $"Service '{command.Id}' not found."));

        service.UpdateServiceDetails(command.Name, command.Description, command.CategoryId,
            command.UnitOfMeasure, command.EstimatedDurationMinutes);
        service.ChangePrice(command.Price, command.Currency);

        await serviceRepository.UpdateAsync(service, ct);
        return Result.Success();
    }
}
