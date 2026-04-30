using CrmSales.SharedKernel;
using CrmSales.Users.Domain.Repositories;

namespace CrmSales.Users.Application.Roles.Commands.RemoveRole;

public static class RemoveRoleFromUserHandler
{
    public static async Task<Result> Handle(
        RemoveRoleFromUserCommand command,
        IRoleRepository roleRepo,
        IUserRepository userRepo,
        CancellationToken ct)
    {
        var user = await userRepo.GetByIdAsync(command.UserId, ct);
        if (user is null)
            return Result.Failure(Error.NotFoundFor("User", command.UserId));

        await roleRepo.RemoveAssignmentAsync(command.UserId, command.RoleId, ct);
        return Result.Success();
    }
}
