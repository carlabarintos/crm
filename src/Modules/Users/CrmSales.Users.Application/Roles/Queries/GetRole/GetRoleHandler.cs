using CrmSales.SharedKernel;
using CrmSales.Users.Application.Roles.DTOs;
using CrmSales.Users.Domain.Repositories;

namespace CrmSales.Users.Application.Roles.Queries.GetRole;

public static class GetRoleHandler
{
    public static async Task<Result<RoleDto>> Handle(
        GetRoleQuery query,
        IRoleRepository repo,
        CancellationToken ct)
    {
        var role = await repo.GetByIdAsync(query.RoleId, ct);
        if (role is null)
            return Result.Failure<RoleDto>(Error.NotFoundFor("Role", query.RoleId));

        return Result.Success(new RoleDto(
            role.Id, role.Name, role.Description,
            role.Permissions.Select(p => p.Permission).ToList(),
            role.CreatedAt, role.UpdatedAt));
    }
}
