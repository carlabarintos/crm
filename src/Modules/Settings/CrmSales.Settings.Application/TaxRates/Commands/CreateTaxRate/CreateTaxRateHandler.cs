using CrmSales.Settings.Domain.Entities;
using CrmSales.Settings.Domain.Repositories;
using CrmSales.SharedKernel;

namespace CrmSales.Settings.Application.TaxRates.Commands.CreateTaxRate;

public static class CreateTaxRateHandler
{
    public static async Task<Result<Guid>> Handle(
        CreateTaxRateCommand command,
        ITaxRateRepository repository,
        CancellationToken ct)
    {
        if (await repository.NameExistsAsync(command.Name, null, ct))
            return Result.Failure<Guid>(new Error("TaxRate.DuplicateName",
                $"A tax rate named '{command.Name}' already exists."));

        var taxRate = TaxRate.Create(command.Name, command.Rate, command.Description, command.Region);
        await repository.AddAsync(taxRate, ct);
        return Result.Success(taxRate.Id);
    }
}
