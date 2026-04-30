using CrmSales.SharedKernel.Domain;

namespace CrmSales.Users.Domain.Entities;

public sealed class UserRoleAssignment : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public DateTime AssignedAt { get; private set; }

    private UserRoleAssignment() { }

    public static UserRoleAssignment Create(Guid userId, Guid roleId)
        => new() { Id = Guid.NewGuid(), UserId = userId, RoleId = roleId, AssignedAt = DateTime.UtcNow };
}
