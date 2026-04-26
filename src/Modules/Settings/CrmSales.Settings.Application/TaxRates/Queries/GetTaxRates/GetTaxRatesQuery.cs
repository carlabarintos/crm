using CrmSales.Settings.Application.TaxRates.DTOs;
using CrmSales.SharedKernel.Application;

namespace CrmSales.Settings.Application.TaxRates.Queries.GetTaxRates;

public record GetTaxRatesQuery(bool? IsActive = null) : IQuery<List<TaxRateDto>>;
