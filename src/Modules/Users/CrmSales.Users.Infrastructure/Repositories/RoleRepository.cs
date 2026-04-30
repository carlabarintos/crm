using CrmSales.Users.Domain.Entities;
using CrmSales.Users.Domain.Repositories;
using CrmSales.Users.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Users.Infrastructure.Repositories;

internal sealed class RoleRepository(UsersDbContext db) : IRoleRepository
{
    public async Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default) =>
        await db.Roles.Include(r => r.Permissions).AsNoTracking().ToListAsync(ct);

    public async Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default) =>
        await db.Roles.AnyAsync(r => r.Name == name && (excludeId == null || r.Id != excludeId), ct);

    public async Task AddAsync(Role role, CancellationToken ct = default)
    {
        db.Roles.Add(role);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Role role, CancellationToken ct = default)
    {
        db.Roles.Update(role);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Role role, CancellationToken ct = default)
    {
        db.Roles.Remove(role);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetPermissionsForUserAsync(string keycloakId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.KeycloakId == keycloakId, ct);

        if (user is null) return [];

        return await db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Join(db.RolePermissions, ur => ur.RoleId, rp => rp.RoleId, (_, rp) => rp.Permission)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Role>> GetRolesForUserAsync(Guid userId, CancellationToken ct = default) =>
        await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(db.Roles.Include(r => r.Permissions), ur => ur.RoleId, r => r.Id, (_, r) => r)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task AddAssignmentAsync(UserRoleAssignment assignment, CancellationToken ct = default)
    {
        db.UserRoles.Add(assignment);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAssignmentAsync(Guid userId, Guid roleId, CancellationToken ct = default)
    {
        var assignment = await db.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId, ct);
        if (assignment is not null)
        {
            db.UserRoles.Remove(assignment);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> AssignmentExistsAsync(Guid userId, Guid roleId, CancellationToken ct = default) =>
        await db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId, ct);
}
