using CrmSales.SharedKernel.Domain;

namespace CrmSales.Users.Domain.Entities;

public sealed class Role : AggregateRoot<Guid>
{
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<RolePermission> _permissions = [];
    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();

    private Role() { Name = string.Empty; }

    public static Role Create(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Role name is required.", nameof(name));

        return new Role
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Role name is required.", nameof(name));

        Name = name.Trim();
        Description = description?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPermissions(IEnumerable<string> permissions)
    {
        _permissions.Clear();
        foreach (var p in permissions.Distinct())
            _permissions.Add(RolePermission.Create(Id, p));
        UpdatedAt = DateTime.UtcNow;
    }
}
