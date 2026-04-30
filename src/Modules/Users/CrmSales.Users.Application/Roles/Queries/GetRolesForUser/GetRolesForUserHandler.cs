using CrmSales.SharedKernel;
using CrmSales.Users.Application.Roles.DTOs;
using CrmSales.Users.Domain.Repositories;

namespace CrmSales.Users.Application.Roles.Queries.GetRolesForUser;

public static class GetRolesForUserHandler
{
    public static async Task<Result<List<UserRoleDto>>> Handle(
        GetRolesForUserQuery query,
        IRoleRepository repo,
        CancellationToken ct)
    {
        var roles = await repo.GetRolesForUserAsync(query.UserId, ct);
        var dtos = roles
            .Select(r => new UserRoleDto(r.Id, r.Name, r.Description, DateTime.MinValue))
            .ToList();
        return Result.Success(dtos);
    }
}
