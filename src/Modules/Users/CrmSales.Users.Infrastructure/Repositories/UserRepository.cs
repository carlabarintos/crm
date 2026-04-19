using CrmSales.Users.Domain.Entities;
using CrmSales.Users.Domain.Repositories;
using CrmSales.Users.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Users.Infrastructure.Repositories;

internal sealed class UserRepository(UsersDbContext dbContext) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default) =>
        await dbContext.Users.OrderBy(u => u.LastName).ToListAsync(ct);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), ct);

    public async Task<User?> GetByKeycloakIdAsync(string keycloakId, CancellationToken ct = default) =>
        await dbContext.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId, ct);

    public async Task<IReadOnlyList<User>> GetByRoleAsync(UserRole role, CancellationToken ct = default) =>
        await dbContext.Users.Where(u => u.Role == role && u.IsActive).ToListAsync(ct);

    public async Task<bool> EmailExistsAsync(string email, Guid? excludeId = null, CancellationToken ct = default) =>
        await dbContext.Users.AnyAsync(u => u.Email == email.ToLowerInvariant()
            && (excludeId == null || u.Id != excludeId), ct);

    public async Task AddAsync(User aggregate, CancellationToken ct = default)
    {
        await dbContext.Users.AddAsync(aggregate, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(User aggregate, CancellationToken ct = default)
    {
        dbContext.Users.Update(aggregate);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(User aggregate, CancellationToken ct = default)
    {
        dbContext.Users.Remove(aggregate);
        await dbContext.SaveChangesAsync(ct);
    }
}
