using CrmSales.SharedKernel.Application;
using CrmSales.Users.Application.Roles.DTOs;

namespace CrmSales.Users.Application.Roles.Queries.GetRole;

public record GetRoleQuery(Guid RoleId) : IQuery<RoleDto>;
