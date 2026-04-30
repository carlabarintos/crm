using CrmSales.SharedKernel.Application;
using CrmSales.Users.Application.Roles.DTOs;

namespace CrmSales.Users.Application.Roles.Queries.GetRolesForUser;

public record GetRolesForUserQuery(Guid UserId) : IQuery<List<UserRoleDto>>;
