using CrmSales.Products.Application.Services.DTOs;
using CrmSales.Products.Domain.Repositories;
using CrmSales.SharedKernel;
using CrmSales.SharedKernel.Application;

namespace CrmSales.Products.Application.Services.Queries.GetServices;

public record GetServicesQuery(
    string? SearchTerm = null,
    bool? IsActive = null,
    int Limit = 20,
    string? Cursor = null) : IQuery<CursorPaginationResult<ServiceDto>>;

public static class GetServicesHandler
{
    public static async Task<Result<CursorPaginationResult<ServiceDto>>> Handle(
        GetServicesQuery query,
        IServiceRepository serviceRepository,
        IProductCategoryRepository categoryRepository,
        CancellationToken ct)
    {
        var result = await serviceRepository.SearchAsync(
            query.SearchTerm, query.IsActive, query.Limit, query.Cursor, ct);

        var categories = await categoryRepository.GetAllAsync(ct);
        var categoryMap = categories.ToDictionary(c => c.Id, c => c.Name);

        var dtos = result.Items.Select(s => new ServiceDto(
            s.Id, s.Name, s.Description, s.ServiceCode.Value,
            s.Price.Amount, s.Price.Currency,
            s.CategoryId, categoryMap.GetValueOrDefault(s.CategoryId),
            s.IsActive, s.UnitOfMeasure, s.EstimatedDurationMinutes,
            s.CreatedAt, s.UpdatedAt)).ToList();

        return Result.Success(CursorPaginationResult<ServiceDto>.Create(dtos, result.NextCursor));
    }
}
