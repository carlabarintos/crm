using CrmSales.Settings.Application.EmailTemplates.DTOs;
using CrmSales.Settings.Domain.Repositories;
using CrmSales.SharedKernel;

namespace CrmSales.Settings.Application.EmailTemplates.Queries.GetEmailTemplates;

public static class GetEmailTemplatesHandler
{
    public static async Task<Result<List<EmailTemplateDto>>> Handle(
        GetEmailTemplatesQuery query,
        IEmailTemplateRepository repo,
        CancellationToken ct)
    {
        var templates = await repo.GetAllAsync(ct);
        var dtos = templates
            .Select(t => new EmailTemplateDto(t.Id, t.TemplateType.ToString(), t.Subject, t.BodyHtml, t.IsActive, t.UpdatedAt))
            .ToList();
        return Result.Success(dtos);
    }
}
