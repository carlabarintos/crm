using CrmSales.SharedKernel.Application;
using CrmSales.Users.Application.Roles.DTOs;

namespace CrmSales.Users.Application.Roles.Queries.GetRoles;

public record GetRolesQuery : IQuery<List<RoleDto>>;
