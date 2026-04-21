using CrmSales.Contacts.Domain.Entities;
using CrmSales.SharedKernel.Application;
using CrmSales.SharedKernel.Domain;

namespace CrmSales.Contacts.Domain.Repositories;

public record ContactSummaryData(int Total, int Active);

public interface IContactRepository : IRepository<Contact, Guid>
{
    Task<IReadOnlyList<Contact>> SearchAsync(string? search, CancellationToken ct = default);
    Task<CursorPaginationResult<Contact>> SearchPagedAsync(string? search, int limit, string? cursor, CancellationToken ct = default);
    Task<ContactSummaryData> GetSummaryAsync(CancellationToken ct = default);
}
