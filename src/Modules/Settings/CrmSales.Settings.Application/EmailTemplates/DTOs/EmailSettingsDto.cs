using CrmSales.Settings.Domain.Enums;

namespace CrmSales.Settings.Application.EmailTemplates.DTOs;

public record EmailSettingsDto(
    Guid? Id,
    string Host,
    int Port,
    string Username,
    string FromName,
    string FromAddress,
    bool EnableSsl,
    bool IsEnabled,
    bool HasPassword,
    SmtpAuthMode AuthMode = SmtpAuthMode.UsernamePassword);
