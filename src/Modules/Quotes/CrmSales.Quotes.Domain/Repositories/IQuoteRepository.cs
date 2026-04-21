using CrmSales.Quotes.Domain.Entities;
using CrmSales.SharedKernel.Application;
using CrmSales.SharedKernel.Domain;

namespace CrmSales.Quotes.Domain.Repositories;

public record QuoteSummaryData(
    int Total, int Draft, int Sent, int Accepted, int Rejected, int Expired,
    decimal SentValue, decimal AcceptedValue, string Currency);

public interface IQuoteRepository : IRepository<Quote, Guid>
{
    Task<IReadOnlyList<Quote>> GetByOpportunityAsync(Guid opportunityId, CancellationToken ct = default);
    Task<IReadOnlyList<Quote>> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default);
    Task<Quote?> GetByNumberAsync(string quoteNumber, CancellationToken ct = default);
    Task<CursorPaginationResult<Quote>> SearchPagedAsync(string? search, string? status, Guid? opportunityId, int limit, string? cursor, CancellationToken ct = default);
    Task<QuoteSummaryData> GetSummaryAsync(CancellationToken ct = default);
}
