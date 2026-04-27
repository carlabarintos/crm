namespace CrmSales.Settings.Application.EmailTemplates.DTOs;

public record EmailTemplateDto(
    Guid Id,
    string TemplateType,
    string Subject,
    string BodyHtml,
    bool IsActive,
    DateTime UpdatedAt);
