using CrmSales.SharedKernel.Domain;

namespace CrmSales.Users.Domain.Entities;

public sealed class RolePermission : Entity<Guid>
{
    public Guid RoleId { get; private set; }
    public string Permission { get; private set; }

    private RolePermission() { Permission = string.Empty; }

    internal static RolePermission Create(Guid roleId, string permission)
        => new() { Id = Guid.NewGuid(), RoleId = roleId, Permission = permission };
}
