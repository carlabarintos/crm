using CrmSales.Products.Application.Services.DTOs;
using CrmSales.Products.Domain.Repositories;
using CrmSales.SharedKernel;
using CrmSales.SharedKernel.Application;

namespace CrmSales.Products.Application.Services.Queries.GetServiceById;

public record GetServiceByIdQuery(Guid Id) : IQuery<ServiceDto>;

public static class GetServiceByIdHandler
{
    public static async Task<Result<ServiceDto>> Handle(
        GetServiceByIdQuery query,
        IServiceRepository serviceRepository,
        IProductCategoryRepository categoryRepository,
        CancellationToken ct)
    {
        var service = await serviceRepository.GetByIdAsync(query.Id, ct);
        if (service is null)
            return Result.Failure<ServiceDto>(new Error("Service.NotFound", $"Service '{query.Id}' not found."));

        var category = await categoryRepository.GetByIdAsync(service.CategoryId, ct);

        return Result.Success(new ServiceDto(
            service.Id, service.Name, service.Description, service.ServiceCode.Value,
            service.Price.Amount, service.Price.Currency,
            service.CategoryId, category?.Name,
            service.IsActive, service.UnitOfMeasure, service.EstimatedDurationMinutes,
            service.CreatedAt, service.UpdatedAt));
    }
}
