using CrmSales.Orders.Domain.Entities;
using CrmSales.Orders.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CrmSales.Api.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders")
            .WithTags("Orders")
            .RequireAuthorization();

        group.MapGet("/", async (
            [FromQuery] OrderStatus? status,
            [FromQuery] Guid? customerId,
            IOrderRepository repo, CancellationToken ct) =>
        {
            var orders = status.HasValue
                ? await repo.GetByStatusAsync(status.Value, ct)
                : customerId.HasValue
                    ? await repo.GetByCustomerAsync(customerId.Value, ct)
                    : await repo.GetAllAsync(ct);

            return Results.Ok(orders.Select(o => new
            {
                o.Id, o.OrderNumber, o.QuoteId,
                Status = o.Status.ToString(), o.TotalAmount, o.Currency,
                o.CreatedAt, o.ShippedAt, o.DeliveredAt
            }));
        });

        group.MapGet("/{id:guid}", async (Guid id, IOrderRepository repo, CancellationToken ct) =>
        {
            var order = await repo.GetByIdAsync(id, ct);
            return order is null ? Results.NotFound() : Results.Ok(new
            {
                order.Id, order.OrderNumber, order.QuoteId,
                Status = order.Status.ToString(),
                order.TotalAmount, order.Currency,
                order.ShippingAddress, order.Notes,
                LineItems = order.LineItems.Select(l => new
                {
                    l.Id, l.ProductId, l.ProductName, l.Quantity, l.UnitPrice, l.LineTotal
                }),
                order.CreatedAt, order.ShippedAt, order.DeliveredAt
            });
        });

        group.MapPost("/{id:guid}/confirm", async (Guid id, IOrderRepository repo, CancellationToken ct) =>
        {
            var order = await repo.GetByIdAsync(id, ct);
            if (order is null) return Results.NotFound();
            order.Confirm();
            await repo.UpdateAsync(order, ct);
            return Results.Ok(new { order.Id, Status = order.Status.ToString() });
        });

        group.MapPost("/{id:guid}/process", async (Guid id, IOrderRepository repo, CancellationToken ct) =>
        {
            var order = await repo.GetByIdAsync(id, ct);
            if (order is null) return Results.NotFound();
            order.StartProcessing();
            await repo.UpdateAsync(order, ct);
            return Results.Ok(new { order.Id, Status = order.Status.ToString() });
        });

        group.MapPost("/{id:guid}/ship", async (
            Guid id,
            [FromBody] ShipOrderRequest req,
            IOrderRepository repo, CancellationToken ct) =>
        {
            var order = await repo.GetByIdAsync(id, ct);
            if (order is null) return Results.NotFound();
            order.Ship(req.TrackingInfo);
            await repo.UpdateAsync(order, ct);
            return Results.Ok(new { order.Id, Status = order.Status.ToString(), order.ShippedAt });
        });

        group.MapPost("/{id:guid}/deliver", async (Guid id, IOrderRepository repo, CancellationToken ct) =>
        {
            var order = await repo.GetByIdAsync(id, ct);
            if (order is null) return Results.NotFound();
            order.Deliver();
            await repo.UpdateAsync(order, ct);
            return Results.Ok(new { order.Id, Status = order.Status.ToString(), order.DeliveredAt });
        });

        group.MapPost("/{id:guid}/cancel", async (
            Guid id,
            [FromBody] CancelOrderRequest req,
            IOrderRepository repo, CancellationToken ct) =>
        {
            var order = await repo.GetByIdAsync(id, ct);
            if (order is null) return Results.NotFound();
            order.Cancel(req.Reason);
            await repo.UpdateAsync(order, ct);
            return Results.Ok(new { order.Id, Status = order.Status.ToString() });
        });

        return app;
    }
}

record ShipOrderRequest(string? TrackingInfo);
record CancelOrderRequest(string Reason);
