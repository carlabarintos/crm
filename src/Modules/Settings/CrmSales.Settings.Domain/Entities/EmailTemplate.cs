using CrmSales.Settings.Domain.Enums;
using CrmSales.SharedKernel.Domain;

namespace CrmSales.Settings.Domain.Entities;

public sealed class EmailTemplate : AggregateRoot<Guid>
{
    public EmailTemplateType TemplateType { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string BodyHtml { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private EmailTemplate() { }

    public static EmailTemplate Create(EmailTemplateType type, string subject, string bodyHtml)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject is required.", nameof(subject));
        if (string.IsNullOrWhiteSpace(bodyHtml))
            throw new ArgumentException("Body is required.", nameof(bodyHtml));

        return new EmailTemplate
        {
            Id = Guid.NewGuid(),
            TemplateType = type,
            Subject = subject.Trim(),
            BodyHtml = bodyHtml.Trim(),
            IsActive = true,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Update(string subject, string bodyHtml)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject is required.", nameof(subject));
        if (string.IsNullOrWhiteSpace(bodyHtml))
            throw new ArgumentException("Body is required.", nameof(bodyHtml));
        Subject = subject.Trim();
        BodyHtml = bodyHtml.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate() { IsActive = true; UpdatedAt = DateTime.UtcNow; }
    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.UtcNow; }
}
