using CrmSales.Settings.Domain.Enums;
using CrmSales.SharedKernel;
using CrmSales.SharedKernel.Application;

namespace CrmSales.Settings.Application.EmailTemplates.Commands.SaveEmailSettings;

public record SaveEmailSettingsCommand(
    string Host,
    int Port,
    string Username,
    string? Password,
    string FromName,
    string FromAddress,
    bool EnableSsl,
    bool IsEnabled,
    SmtpAuthMode AuthMode = SmtpAuthMode.UsernamePassword) : ICommand<Result>;
