using FluentValidation;

namespace CrmSales.Settings.Application.EmailTemplates.Commands.SaveEmailSettings;

public class SaveEmailSettingsValidator : AbstractValidator<SaveEmailSettingsCommand>
{
    public SaveEmailSettingsValidator()
    {
        RuleFor(x => x.Host).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Port).InclusiveBetween(1, 65535);
        RuleFor(x => x.FromAddress).NotEmpty().EmailAddress().MaximumLength(500);
        RuleFor(x => x.FromName).NotEmpty().MaximumLength(500);
    }
}
