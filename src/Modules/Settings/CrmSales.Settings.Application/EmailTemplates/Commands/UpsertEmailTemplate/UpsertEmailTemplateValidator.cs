using FluentValidation;

namespace CrmSales.Settings.Application.EmailTemplates.Commands.UpsertEmailTemplate;

public class UpsertEmailTemplateValidator : AbstractValidator<UpsertEmailTemplateCommand>
{
    public UpsertEmailTemplateValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(500);
        RuleFor(x => x.BodyHtml).NotEmpty();
    }
}
