using CrmSales.Settings.Application.EmailTemplates.DTOs;
using CrmSales.SharedKernel;
using CrmSales.SharedKernel.Application;

namespace CrmSales.Settings.Application.EmailTemplates.Queries.GetEmailSettings;

public record GetEmailSettingsQuery : IQuery<Result<EmailSettingsDto>>;
