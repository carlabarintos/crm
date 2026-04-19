using CrmSales.Orders.Domain.Entities;
using CrmSales.Orders.Domain.Repositories;
using CrmSales.Orders.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Orders.Infrastructure.Repositories;

internal sealed class OrderRepository(OrdersDbContext dbContext) : IOrderRepository
{
    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Orders.Include(o => o.LineItems)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken ct = default) =>
        await dbContext.Orders.Include(o => o.LineItems)
            .OrderByDescending(o => o.CreatedAt).ToListAsync(ct);

    public async Task<Order?> GetByNumberAsync(string orderNumber, CancellationToken ct = default) =>
        await dbContext.Orders.Include(o => o.LineItems)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, ct);

    public async Task<Order?> GetByQuoteIdAsync(Guid quoteId, CancellationToken ct = default) =>
        await dbContext.Orders.Include(o => o.LineItems)
            .FirstOrDefaultAsync(o => o.QuoteId == quoteId, ct);

    public async Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default) =>
        await dbContext.Orders.Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<Order>> GetByStatusAsync(OrderStatus status, CancellationToken ct = default) =>
        await dbContext.Orders.Where(o => o.Status == status).ToListAsync(ct);

    public async Task AddAsync(Order aggregate, CancellationToken ct = default)
    {
        await dbContext.Orders.AddAsync(aggregate, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Order aggregate, CancellationToken ct = default)
    {
        dbContext.Orders.Update(aggregate);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Order aggregate, CancellationToken ct = default)
    {
        dbContext.Orders.Remove(aggregate);
        await dbContext.SaveChangesAsync(ct);
    }
}
