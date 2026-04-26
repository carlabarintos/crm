using FluentValidation;

namespace CrmSales.Settings.Application.TaxRates.Commands.UpdateTaxRate;

public sealed class UpdateTaxRateValidator : AbstractValidator<UpdateTaxRateCommand>
{
    public UpdateTaxRateValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Rate).InclusiveBetween(0, 100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Region).MaximumLength(100);
    }
}
