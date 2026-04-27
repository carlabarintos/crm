using CrmSales.Settings.Application.EmailTemplates.DTOs;
using CrmSales.SharedKernel;
using CrmSales.SharedKernel.Application;

namespace CrmSales.Settings.Application.EmailTemplates.Queries.GetEmailTemplates;

public record GetEmailTemplatesQuery : IQuery<Result<List<EmailTemplateDto>>>;
