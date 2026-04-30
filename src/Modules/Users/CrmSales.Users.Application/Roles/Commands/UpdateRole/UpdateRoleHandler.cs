using CrmSales.SharedKernel;
using CrmSales.Users.Domain.Repositories;

namespace CrmSales.Users.Application.Roles.Commands.UpdateRole;

public static class UpdateRoleHandler
{
    public static async Task<Result> Handle(
        UpdateRoleCommand command,
        IRoleRepository repo,
        CancellationToken ct)
    {
        var role = await repo.GetByIdAsync(command.RoleId, ct);
        if (role is null)
            return Result.Failure(Error.NotFoundFor("Role", command.RoleId));

        if (await repo.NameExistsAsync(command.Name, command.RoleId, ct))
            return Result.Failure(new Error("Role.DuplicateName",
                $"A role named '{command.Name}' already exists."));

        role.Update(command.Name, command.Description);
        role.SetPermissions(command.Permissions);
        await repo.UpdateAsync(role, ct);
        return Result.Success();
    }
}
