using CrmSales.SharedKernel.Application;

namespace CrmSales.Users.Application.Roles.Commands.RemoveRole;

public record RemoveRoleFromUserCommand(Guid UserId, Guid RoleId) : ICommand;
