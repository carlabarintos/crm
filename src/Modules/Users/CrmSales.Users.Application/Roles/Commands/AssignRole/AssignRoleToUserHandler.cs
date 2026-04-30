using CrmSales.SharedKernel;
using CrmSales.Users.Domain.Entities;
using CrmSales.Users.Domain.Repositories;

namespace CrmSales.Users.Application.Roles.Commands.AssignRole;

public static class AssignRoleToUserHandler
{
    public static async Task<Result> Handle(
        AssignRoleToUserCommand command,
        IRoleRepository roleRepo,
        IUserRepository userRepo,
        CancellationToken ct)
    {
        var user = await userRepo.GetByIdAsync(command.UserId, ct);
        if (user is null)
            return Result.Failure(Error.NotFoundFor("User", command.UserId));

        var role = await roleRepo.GetByIdAsync(command.RoleId, ct);
        if (role is null)
            return Result.Failure(Error.NotFoundFor("Role", command.RoleId));

        if (await roleRepo.AssignmentExistsAsync(command.UserId, command.RoleId, ct))
            return Result.Failure(new Error("UserRole.AlreadyAssigned",
                "This role is already assigned to the user."));

        await roleRepo.AddAssignmentAsync(UserRoleAssignment.Create(command.UserId, command.RoleId), ct);
        return Result.Success();
    }
}
