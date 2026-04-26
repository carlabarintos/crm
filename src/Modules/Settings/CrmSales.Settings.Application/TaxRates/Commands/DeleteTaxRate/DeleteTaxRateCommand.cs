using CrmSales.SharedKernel.Application;

namespace CrmSales.Settings.Application.TaxRates.Commands.DeleteTaxRate;

public record DeleteTaxRateCommand(Guid Id) : ICommand;
