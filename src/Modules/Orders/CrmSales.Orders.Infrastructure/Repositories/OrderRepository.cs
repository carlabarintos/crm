using CrmSales.Orders.Domain.Entities;
using CrmSales.Orders.Domain.Repositories;
using CrmSales.Orders.Infrastructure.Persistence;
using CrmSales.SharedKernel.Application;
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

    public async Task<CursorPaginationResult<Order>> SearchAsync(string? term, OrderStatus? status, int limit, string? cursor, CancellationToken ct = default)
    {
        var query = dbContext.Orders.AsQueryable();

        if (!string.IsNullOrWhiteSpace(term))
            query = query.Where(o => EF.Functions.ILike(o.OrderNumber, $"%{term}%"));

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        if (!string.IsNullOrEmpty(cursor) && Guid.TryParse(cursor, out var cursorId))
            query = query.Where(o => o.Id > cursorId);

        var items = await query
            .OrderBy(o => o.Id)
            .Take(limit + 1)
            .ToListAsync(ct);

        string? nextCursor = null;
        if (items.Count > limit)
        {
            items.RemoveAt(items.Count - 1);
            nextCursor = items[^1].Id.ToString();
        }

        return CursorPaginationResult<Order>.Create(items, nextCursor);
    }

    public async Task<OrderSummaryData> GetSummaryAsync(CancellationToken ct = default)
    {
        var counts = await dbContext.Orders
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total      = g.Count(),
                Pending    = g.Count(o => o.Status == OrderStatus.Pending),
                Confirmed  = g.Count(o => o.Status == OrderStatus.Confirmed),
                Processing = g.Count(o => o.Status == OrderStatus.Processing),
                Shipped    = g.Count(o => o.Status == OrderStatus.Shipped),
                Delivered  = g.Count(o => o.Status == OrderStatus.Delivered),
                Cancelled  = g.Count(o => o.Status == OrderStatus.Cancelled),
            })
            .FirstOrDefaultAsync(ct);

        var deliveredRevenue = await dbContext.Orders
            .Where(o => o.Status == OrderStatus.Delivered)
            .Join(dbContext.LineItems, o => o.Id, l => l.OrderId,
                (_, l) => l.UnitPrice * l.Quantity)
            .SumAsync(ct);

        var monthlyRaw = await dbContext.Orders
            .Where(o => o.Status == OrderStatus.Delivered)
            .Join(dbContext.LineItems, o => o.Id, l => l.OrderId,
                (o, l) => new { o.CreatedAt.Year, o.CreatedAt.Month, Revenue = l.UnitPrice * l.Quantity })
            .GroupBy(x => new { x.Year, x.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Revenue = g.Sum(x => x.Revenue) })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(ct);

        var currency = await dbContext.Orders
            .Select(o => o.Currency).FirstOrDefaultAsync(ct) ?? "USD";

        int active = (counts?.Confirmed ?? 0) + (counts?.Processing ?? 0) + (counts?.Shipped ?? 0);

        return new OrderSummaryData(
            counts?.Total ?? 0, counts?.Pending ?? 0, active,
            counts?.Delivered ?? 0, counts?.Cancelled ?? 0,
            deliveredRevenue, currency,
            monthlyRaw.Select(m => new MonthlyRevenueData(m.Year, m.Month, m.Revenue)).ToList());
    }

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
