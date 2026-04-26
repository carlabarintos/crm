using CrmSales.Settings.Domain.Repositories;
using CrmSales.SharedKernel;

namespace CrmSales.Settings.Application.TaxRates.Commands.DeleteTaxRate;

public static class DeleteTaxRateHandler
{
    public static async Task<Result> Handle(
        DeleteTaxRateCommand command,
        ITaxRateRepository repository,
        CancellationToken ct)
    {
        var taxRate = await repository.GetByIdAsync(command.Id, ct);
        if (taxRate is null)
            return Result.Failure(Error.NotFoundFor("TaxRate", command.Id));

        await repository.DeleteAsync(taxRate, ct);
        return Result.Success();
    }
}
