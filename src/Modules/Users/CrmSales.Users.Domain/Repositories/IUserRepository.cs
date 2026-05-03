using CrmSales.SharedKernel.Domain;
using CrmSales.Users.Domain.Entities;

namespace CrmSales.Users.Domain.Repositories;

public interface IUserRepository : IRepository<User, Guid>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByKeycloakIdAsync(string keycloakId, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, Guid? excludeId = null, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}
