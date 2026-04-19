using CrmSales.Orders.Domain.Entities;
using CrmSales.SharedKernel.Domain;

namespace CrmSales.Orders.Domain.Repositories;

public interface IOrderRepository : IRepository<Order, Guid>
{
    Task<Order?> GetByNumberAsync(string orderNumber, CancellationToken ct = default);
    Task<Order?> GetByQuoteIdAsync(Guid quoteId, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetByStatusAsync(OrderStatus status, CancellationToken ct = default);
}
