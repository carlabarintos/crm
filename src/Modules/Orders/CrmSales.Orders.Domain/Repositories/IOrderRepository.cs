using CrmSales.Orders.Domain.Entities;
using CrmSales.SharedKernel.Application;
using CrmSales.SharedKernel.Domain;

namespace CrmSales.Orders.Domain.Repositories;

public record MonthlyRevenueData(int Year, int Month, decimal Revenue);
public record OrderSummaryData(
    int Total, int Pending, int Active, int Delivered, int Cancelled,
    decimal DeliveredRevenue, string Currency,
    List<MonthlyRevenueData> MonthlyRevenue);

public interface IOrderRepository : IRepository<Order, Guid>
{
    Task<Order?> GetByNumberAsync(string orderNumber, CancellationToken ct = default);
    Task<Order?> GetByQuoteIdAsync(Guid quoteId, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetByStatusAsync(OrderStatus status, CancellationToken ct = default);
    Task<CursorPaginationResult<Order>> SearchAsync(string? term, OrderStatus? status, int limit, string? cursor, CancellationToken ct = default);
    Task<OrderSummaryData> GetSummaryAsync(int? year = null, int? month = null, CancellationToken ct = default);
}
