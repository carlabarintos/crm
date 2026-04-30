using CrmSales.SharedKernel;
using CrmSales.Users.Domain.Repositories;

namespace CrmSales.Users.Application.Roles.Commands.DeleteRole;

public static class DeleteRoleHandler
{
    public static async Task<Result> Handle(
        DeleteRoleCommand command,
        IRoleRepository repo,
        CancellationToken ct)
    {
        var role = await repo.GetByIdAsync(command.RoleId, ct);
        if (role is null)
            return Result.Failure(Error.NotFoundFor("Role", command.RoleId));

        await repo.DeleteAsync(role, ct);
        return Result.Success();
    }
}
