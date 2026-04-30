using CrmSales.SharedKernel.Application;

namespace CrmSales.Users.Application.Roles.Commands.CreateRole;

public record CreateRoleCommand(
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions) : ICommand<Guid>;
