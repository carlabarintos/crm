using CrmSales.SharedKernel.Application;

namespace CrmSales.Settings.Application.TaxRates.Commands.SetDefaultTaxRate;

public record SetDefaultTaxRateCommand(Guid Id) : ICommand;
