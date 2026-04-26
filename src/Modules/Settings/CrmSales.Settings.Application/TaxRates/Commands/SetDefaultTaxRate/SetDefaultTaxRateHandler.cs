using CrmSales.Settings.Domain.Repositories;
using CrmSales.SharedKernel;

namespace CrmSales.Settings.Application.TaxRates.Commands.SetDefaultTaxRate;

public static class SetDefaultTaxRateHandler
{
    public static async Task<Result> Handle(
        SetDefaultTaxRateCommand command,
        ITaxRateRepository repository,
        CancellationToken ct)
    {
        var taxRate = await repository.GetByIdAsync(command.Id, ct);
        if (taxRate is null)
            return Result.Failure(Error.NotFoundFor("TaxRate", command.Id));

        var currentDefault = await repository.GetDefaultAsync(ct);
        if (currentDefault is not null && currentDefault.Id != taxRate.Id)
        {
            currentDefault.ClearDefault();
            await repository.UpdateAsync(currentDefault, ct);
        }

        taxRate.SetAsDefault();
        await repository.UpdateAsync(taxRate, ct);
        return Result.Success();
    }
}
