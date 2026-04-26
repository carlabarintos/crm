using CrmSales.SharedKernel.Application;

namespace CrmSales.Settings.Application.TaxRates.Commands.CreateTaxRate;

public record CreateTaxRateCommand(
    string Name,
    decimal Rate,
    string? Description,
    string? Region) : ICommand<Guid>;
