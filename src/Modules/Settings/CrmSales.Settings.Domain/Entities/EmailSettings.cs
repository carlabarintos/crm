using CrmSales.Settings.Domain.Enums;
using CrmSales.SharedKernel.Domain;

namespace CrmSales.Settings.Domain.Entities;

public sealed class EmailSettings : AggregateRoot<Guid>
{
    public string Host { get; private set; } = string.Empty;
    public int Port { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string Password { get; private set; } = string.Empty;
    public string FromName { get; private set; } = string.Empty;
    public string FromAddress { get; private set; } = string.Empty;
    public bool EnableSsl { get; private set; }
    public bool IsEnabled { get; private set; }
    public SmtpAuthMode AuthMode { get; private set; } = SmtpAuthMode.UsernamePassword;
    public DateTime UpdatedAt { get; private set; }

    private EmailSettings() { }

    public static EmailSettings Create(
        string host, int port, string username, string password,
        string fromName, string fromAddress, bool enableSsl,
        SmtpAuthMode authMode = SmtpAuthMode.UsernamePassword)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("SMTP host is required.", nameof(host));
        if (string.IsNullOrWhiteSpace(fromAddress))
            throw new ArgumentException("From address is required.", nameof(fromAddress));

        return new EmailSettings
        {
            Id = Guid.NewGuid(),
            Host = host.Trim(),
            Port = port,
            Username = username.Trim(),
            Password = password.Trim(),
            FromName = fromName.Trim(),
            FromAddress = fromAddress.Trim(),
            EnableSsl = enableSsl,
            IsEnabled = true,
            AuthMode = authMode,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string host, int port, string username, string? password,
        string fromName, string fromAddress, bool enableSsl, bool isEnabled,
        SmtpAuthMode authMode)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("SMTP host is required.", nameof(host));
        if (string.IsNullOrWhiteSpace(fromAddress))
            throw new ArgumentException("From address is required.", nameof(fromAddress));

        Host = host.Trim();
        Port = port;
        Username = username.Trim();
        if (!string.IsNullOrWhiteSpace(password))
            Password = password.Trim();
        FromName = fromName.Trim();
        FromAddress = fromAddress.Trim();
        EnableSsl = enableSsl;
        IsEnabled = isEnabled;
        AuthMode = authMode;
        UpdatedAt = DateTime.UtcNow;
    }
}
