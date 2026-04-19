using CrmSales.Contacts.Domain.Entities;
using CrmSales.SharedKernel.Domain;

namespace CrmSales.Contacts.Domain.Repositories;

public interface IContactRepository : IRepository<Contact, Guid>
{
    Task<IReadOnlyList<Contact>> SearchAsync(string? search, CancellationToken ct = default);
}
