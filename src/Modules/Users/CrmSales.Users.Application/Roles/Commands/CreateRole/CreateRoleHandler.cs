using CrmSales.SharedKernel;
using CrmSales.Users.Domain.Entities;
using CrmSales.Users.Domain.Repositories;

namespace CrmSales.Users.Application.Roles.Commands.CreateRole;

public static class CreateRoleHandler
{
    public static async Task<Result<Guid>> Handle(
        CreateRoleCommand command,
        IRoleRepository repo,
        CancellationToken ct)
    {
        if (await repo.NameExistsAsync(command.Name, null, ct))
            return Result.Failure<Guid>(new Error("Role.DuplicateName",
                $"A role named '{command.Name}' already exists."));

        var role = Role.Create(command.Name, command.Description);
        role.SetPermissions(command.Permissions);
        await repo.AddAsync(role, ct);
        return Result.Success(role.Id);
    }
}
