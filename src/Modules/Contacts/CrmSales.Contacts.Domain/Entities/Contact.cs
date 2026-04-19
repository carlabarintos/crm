using CrmSales.SharedKernel.Domain;

namespace CrmSales.Contacts.Domain.Entities;

public sealed class Contact : AggregateRoot<Guid>
{
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Company { get; private set; }
    public string? JobTitle { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public string FullName => $"{FirstName} {LastName}";

    private Contact()
    {
        FirstName = string.Empty;
        LastName = string.Empty;
    }

    public static Contact Create(
        string firstName, string lastName,
        string? email, string? phone,
        string? company, string? jobTitle, string? notes)
    {
        return new Contact
        {
            Id = Guid.NewGuid(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = email?.Trim().ToLowerInvariant(),
            Phone = phone?.Trim(),
            Company = company?.Trim(),
            JobTitle = jobTitle?.Trim(),
            Notes = notes?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Update(string firstName, string lastName,
        string? email, string? phone,
        string? company, string? jobTitle, string? notes)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email?.Trim().ToLowerInvariant();
        Phone = phone?.Trim();
        Company = company?.Trim();
        JobTitle = jobTitle?.Trim();
        Notes = notes?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.UtcNow; }
    public void Activate() { IsActive = true; UpdatedAt = DateTime.UtcNow; }
}
