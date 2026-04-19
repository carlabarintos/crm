using CrmSales.Quotes.Domain.Entities;
using CrmSales.SharedKernel.Domain;

namespace CrmSales.Quotes.Domain.Repositories;

public interface IQuoteRepository : IRepository<Quote, Guid>
{
    Task<IReadOnlyList<Quote>> GetByOpportunityAsync(Guid opportunityId, CancellationToken ct = default);
    Task<IReadOnlyList<Quote>> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default);
    Task<Quote?> GetByNumberAsync(string quoteNumber, CancellationToken ct = default);
}
