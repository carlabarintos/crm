using CrmSales.Settings.Application.TaxRates.DTOs;
using CrmSales.Settings.Domain.Repositories;
using CrmSales.SharedKernel;

namespace CrmSales.Settings.Application.TaxRates.Queries.GetTaxRates;

public static class GetTaxRatesHandler
{
    public static async Task<Result<List<TaxRateDto>>> Handle(
        GetTaxRatesQuery query,
        ITaxRateRepository repository,
        CancellationToken ct)
    {
        var all = await repository.GetAllAsync(ct);
        var filtered = query.IsActive.HasValue
            ? all.Where(t => t.IsActive == query.IsActive.Value)
            : all;

        var dtos = filtered
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Name)
            .Select(t => new TaxRateDto(
                t.Id, t.Name, t.Rate, t.Description, t.Region,
                t.IsDefault, t.IsActive, t.CreatedAt, t.UpdatedAt))
            .ToList();

        return Result.Success(dtos);
    }
}
