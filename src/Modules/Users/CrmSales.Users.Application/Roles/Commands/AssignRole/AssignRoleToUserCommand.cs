using CrmSales.SharedKernel.Application;

namespace CrmSales.Users.Application.Roles.Commands.AssignRole;

public record AssignRoleToUserCommand(Guid UserId, Guid RoleId) : ICommand;
