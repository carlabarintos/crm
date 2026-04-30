using CrmSales.SharedKernel;
using CrmSales.Users.Application.Roles.DTOs;
using CrmSales.Users.Domain.Repositories;

namespace CrmSales.Users.Application.Roles.Queries.GetRoles;

public static class GetRolesHandler
{
    public static async Task<Result<List<RoleDto>>> Handle(
        GetRolesQuery query,
        IRoleRepository repo,
        CancellationToken ct)
    {
        var roles = await repo.GetAllAsync(ct);
        var dtos = roles
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(
                r.Id, r.Name, r.Description,
                r.Permissions.Select(p => p.Permission).ToList(),
                r.CreatedAt, r.UpdatedAt))
            .ToList();
        return Result.Success(dtos);
    }
}
