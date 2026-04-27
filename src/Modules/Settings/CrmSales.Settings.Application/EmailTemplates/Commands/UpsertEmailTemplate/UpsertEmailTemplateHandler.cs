using CrmSales.Settings.Domain.Entities;
using CrmSales.Settings.Domain.Repositories;
using CrmSales.SharedKernel;

namespace CrmSales.Settings.Application.EmailTemplates.Commands.UpsertEmailTemplate;

public static class UpsertEmailTemplateHandler
{
    public static async Task<Result> Handle(
        UpsertEmailTemplateCommand command,
        IEmailTemplateRepository repo,
        CancellationToken ct)
    {
        var existing = await repo.GetByTypeAsync(command.TemplateType, ct);
        if (existing is null)
        {
            var template = EmailTemplate.Create(command.TemplateType, command.Subject, command.BodyHtml);
            await repo.AddAsync(template, ct);
        }
        else
        {
            existing.Update(command.Subject, command.BodyHtml);
            await repo.UpdateAsync(existing, ct);
        }
        return Result.Success();
    }
}
