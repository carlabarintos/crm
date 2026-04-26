using CrmSales.SharedKernel.Application;

namespace CrmSales.Settings.Application.TaxRates.Commands.UpdateTaxRate;

public record UpdateTaxRateCommand(
    Guid Id,
    string Name,
    decimal Rate,
    string? Description,
    string? Region) : ICommand;
