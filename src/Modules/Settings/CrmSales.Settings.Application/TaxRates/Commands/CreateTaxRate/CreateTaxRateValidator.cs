using FluentValidation;

namespace CrmSales.Settings.Application.TaxRates.Commands.CreateTaxRate;

public sealed class CreateTaxRateValidator : AbstractValidator<CreateTaxRateCommand>
{
    public CreateTaxRateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Rate).InclusiveBetween(0, 100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Region).MaximumLength(100);
    }
}
