using CrmSales.SharedKernel.Domain;
using CrmSales.Users.Domain.Events;

namespace CrmSales.Users.Domain.Entities;

public sealed class User : AggregateRoot<Guid>
{
    public string KeycloakId { get; private set; }
    public string Email { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public string FullName => $"{FirstName} {LastName}";

    private User()
    {
        KeycloakId = string.Empty;
        Email = string.Empty;
        FirstName = string.Empty;
        LastName = string.Empty;
    }

    public static User Create(string keycloakId, string email, string firstName, string lastName)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            KeycloakId = keycloakId,
            Email = email.ToLowerInvariant().Trim(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        user.RaiseDomainEvent(new UserCreatedEvent(user.Id, user.Email, user.FullName));
        return user;
    }

    public void UpdateProfile(string firstName, string lastName)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.UtcNow; }
    public void Activate() { IsActive = true; UpdatedAt = DateTime.UtcNow; }
}
