using CrmSales.SharedKernel.Application;

namespace CrmSales.Users.Application.Roles.Commands.UpdateRole;

public record UpdateRoleCommand(
    Guid RoleId,
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions) : ICommand;
