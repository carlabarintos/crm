using CrmSales.SharedKernel.Application;

namespace CrmSales.Users.Application.Roles.Commands.DeleteRole;

public record DeleteRoleCommand(Guid RoleId) : ICommand;
