using CrmSales.Settings.Application.TaxRates.DTOs;
using CrmSales.Settings.Domain.Repositories;
using CrmSales.SharedKernel;

namespace CrmSales.Settings.Application.TaxRates.Queries.GetTaxRateById;

public static class GetTaxRateByIdHandler
{
    public static async Task<Result<TaxRateDto>> Handle(
        GetTaxRateByIdQuery query,
        ITaxRateRepository repository,
        CancellationToken ct)
    {
        var taxRate = await repository.GetByIdAsync(query.Id, ct);
        if (taxRate is null)
            return Result.Failure<TaxRateDto>(Error.NotFoundFor("TaxRate", query.Id));

        return Result.Success(new TaxRateDto(
            taxRate.Id, taxRate.Name, taxRate.Rate, taxRate.Description, taxRate.Region,
            taxRate.IsDefault, taxRate.IsActive, taxRate.CreatedAt, taxRate.UpdatedAt));
    }
}
