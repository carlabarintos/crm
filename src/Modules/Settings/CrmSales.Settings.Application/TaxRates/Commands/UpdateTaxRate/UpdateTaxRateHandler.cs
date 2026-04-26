using CrmSales.Settings.Domain.Repositories;
using CrmSales.SharedKernel;

namespace CrmSales.Settings.Application.TaxRates.Commands.UpdateTaxRate;

public static class UpdateTaxRateHandler
{
    public static async Task<Result> Handle(
        UpdateTaxRateCommand command,
        ITaxRateRepository repository,
        CancellationToken ct)
    {
        var taxRate = await repository.GetByIdAsync(command.Id, ct);
        if (taxRate is null)
            return Result.Failure(Error.NotFoundFor("TaxRate", command.Id));

        if (await repository.NameExistsAsync(command.Name, command.Id, ct))
            return Result.Failure(new Error("TaxRate.DuplicateName",
                $"A tax rate named '{command.Name}' already exists."));

        taxRate.Update(command.Name, command.Rate, command.Description, command.Region);
        await repository.UpdateAsync(taxRate, ct);
        return Result.Success();
    }
}
