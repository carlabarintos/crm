using CrmSales.Settings.Domain.Enums;
using CrmSales.SharedKernel;
using CrmSales.SharedKernel.Application;

namespace CrmSales.Settings.Application.EmailTemplates.Commands.UpsertEmailTemplate;

public record UpsertEmailTemplateCommand(
    EmailTemplateType TemplateType,
    string Subject,
    string BodyHtml) : ICommand<Result>;
