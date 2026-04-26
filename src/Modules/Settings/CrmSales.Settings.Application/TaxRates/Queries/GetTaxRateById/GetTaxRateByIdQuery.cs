using CrmSales.Settings.Application.TaxRates.DTOs;
using CrmSales.SharedKernel.Application;

namespace CrmSales.Settings.Application.TaxRates.Queries.GetTaxRateById;

public record GetTaxRateByIdQuery(Guid Id) : IQuery<TaxRateDto>;
