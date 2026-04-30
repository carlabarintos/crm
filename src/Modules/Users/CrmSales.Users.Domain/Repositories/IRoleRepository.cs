using CrmSales.Users.Domain.Entities;

namespace CrmSales.Users.Domain.Repositories;

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default);
    Task AddAsync(Role role, CancellationToken ct = default);
    Task UpdateAsync(Role role, CancellationToken ct = default);
    Task DeleteAsync(Role role, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetPermissionsForUserAsync(string keycloakId, CancellationToken ct = default);
    Task<IReadOnlyList<Role>> GetRolesForUserAsync(Guid userId, CancellationToken ct = default);
    Task AddAssignmentAsync(UserRoleAssignment assignment, CancellationToken ct = default);
    Task RemoveAssignmentAsync(Guid userId, Guid roleId, CancellationToken ct = default);
    Task<bool> AssignmentExistsAsync(Guid userId, Guid roleId, CancellationToken ct = default);
}
