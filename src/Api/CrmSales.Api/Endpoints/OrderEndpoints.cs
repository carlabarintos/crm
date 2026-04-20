using CrmSales.Api.Auditing;
using CrmSales.SharedKernel.MultiTenancy;
using CrmSales.Api.Notifications;
using CrmSales.Orders.Domain.Entities;
using CrmSales.Orders.Domain.Repositories;
using CrmSales.Products.Domain.Repositories;
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

        group.MapPost("/{id:guid}/confirm", async (
            Guid id,
            HttpContext http,
            IOrderRepository repo,
            INotificationBroadcaster broadcaster,
            IAuditService audit,
            ITenantContext tenant,
            CancellationToken ct) =>
        {
            var actor = http.User.FindFirst("preferred_username")?.Value ?? "system";
            var order = await repo.GetByIdAsync(id, ct);
            if (order is null) return Results.NotFound();
            order.Confirm();
            await repo.UpdateAsync(order, ct);

            var msg = $"Order {order.OrderNumber} confirmed by {actor}";
            await broadcaster.BroadcastAsync(new NotificationEvent(
                "order.confirmed", "Order Confirmed", msg,
                order.Id.ToString(), actor, tenant.TenantId, DateTime.UtcNow), ct);
            await audit.LogAsync(tenant.TenantId, "order.confirmed", "Order",
                order.Id.ToString(), msg, actor, ct);

            return Results.Ok(new { order.Id, Status = order.Status.ToString() });
        });

        group.MapPost("/{id:guid}/process", async (
            Guid id,
            HttpContext http,
            IOrderRepository repo,
            INotificationBroadcaster broadcaster,
            IAuditService audit,
            ITenantContext tenant,
            CancellationToken ct) =>
        {
            var actor = http.User.FindFirst("preferred_username")?.Value ?? "system";
            var order = await repo.GetByIdAsync(id, ct);
            if (order is null) return Results.NotFound();
            order.StartProcessing();
            await repo.UpdateAsync(order, ct);

            var msg = $"Order {order.OrderNumber} started processing by {actor}";
            await broadcaster.BroadcastAsync(new NotificationEvent(
                "order.processing", "Order Processing", msg,
                order.Id.ToString(), actor, tenant.TenantId, DateTime.UtcNow), ct);
            await audit.LogAsync(tenant.TenantId, "order.processing", "Order",
                order.Id.ToString(), msg, actor, ct);

            return Results.Ok(new { order.Id, Status = order.Status.ToString() });
        });

        group.MapPost("/{id:guid}/ship", async (
            Guid id,
            [FromBody] ShipOrderRequest req,
            HttpContext http,
            IOrderRepository repo,
            INotificationBroadcaster broadcaster,
            IAuditService audit,
            ITenantContext tenant,
            CancellationToken ct) =>
        {
            var actor = http.User.FindFirst("preferred_username")?.Value ?? "system";
            var order = await repo.GetByIdAsync(id, ct);
            if (order is null) return Results.NotFound();
            order.Ship(req.TrackingInfo);
            await repo.UpdateAsync(order, ct);

            var msg = $"Order {order.OrderNumber} shipped by {actor}" +
                      (string.IsNullOrWhiteSpace(req.TrackingInfo) ? "" : $" — tracking: {req.TrackingInfo}");
            await broadcaster.BroadcastAsync(new NotificationEvent(
                "order.shipped", "Order Shipped", msg,
                order.Id.ToString(), actor, tenant.TenantId, DateTime.UtcNow), ct);
            await audit.LogAsync(tenant.TenantId, "order.shipped", "Order",
                order.Id.ToString(), msg, actor, ct);

            return Results.Ok(new { order.Id, Status = order.Status.ToString(), order.ShippedAt });
        });

        group.MapPost("/{id:guid}/deliver", async (
            Guid id,
            HttpContext http,
            IOrderRepository repo,
            IProductRepository productRepo,
            INotificationBroadcaster broadcaster,
            IAuditService audit,
            ITenantContext tenant,
            CancellationToken ct) =>
        {
            var actor = http.User.FindFirst("preferred_username")?.Value ?? "system";
            var order = await repo.GetByIdAsync(id, ct);
            if (order is null) return Results.NotFound();
            order.Deliver();
            await repo.UpdateAsync(order, ct);

            foreach (var item in order.LineItems)
            {
                var product = await productRepo.GetByIdAsync(item.ProductId, ct);
                if (product is not null)
                {
                    product.AdjustStock(-item.Quantity);
                    await productRepo.UpdateAsync(product, ct);
                }
            }

            var msg = $"Order {order.OrderNumber} delivered by {actor}";
            await broadcaster.BroadcastAsync(new NotificationEvent(
                "order.delivered", "Order Delivered", msg,
                order.Id.ToString(), actor, tenant.TenantId, DateTime.UtcNow), ct);
            await audit.LogAsync(tenant.TenantId, "order.delivered", "Order",
                order.Id.ToString(), msg, actor, ct);

            return Results.Ok(new { order.Id, Status = order.Status.ToString(), order.DeliveredAt });
        });

        group.MapPost("/{id:guid}/cancel", async (
            Guid id,
            [FromBody] CancelOrderRequest req,
            HttpContext http,
            IOrderRepository repo,
            INotificationBroadcaster broadcaster,
            IAuditService audit,
            ITenantContext tenant,
            CancellationToken ct) =>
        {
            var actor = http.User.FindFirst("preferred_username")?.Value ?? "system";
            var order = await repo.GetByIdAsync(id, ct);
            if (order is null) return Results.NotFound();
            order.Cancel(req.Reason);
            await repo.UpdateAsync(order, ct);

            var msg = $"Order {order.OrderNumber} cancelled by {actor}: {req.Reason}";
            await broadcaster.BroadcastAsync(new NotificationEvent(
                "order.cancelled", "Order Cancelled", msg,
                order.Id.ToString(), actor, tenant.TenantId, DateTime.UtcNow), ct);
            await audit.LogAsync(tenant.TenantId, "order.cancelled", "Order",
                order.Id.ToString(), msg, actor, ct);

            return Results.Ok(new { order.Id, Status = order.Status.ToString() });
        });

        group.MapPost("/{id:guid}/line-items", async (
            Guid id,
            [FromBody] AddOrderLineItemRequest req,
            IOrderRepository repo, CancellationToken ct) =>
        {
            var order = await repo.GetByIdAsync(id, ct);
            if (order is null) return Results.NotFound();
            order.AddLineItem(req.ProductId, req.ProductName, req.Quantity, req.UnitPrice);
            await repo.UpdateAsync(order, ct);
            return Results.Ok(new { order.Id, order.TotalAmount });
        });

        group.MapPut("/{id:guid}/line-items/{lineItemId:guid}", async (
            Guid id,
            Guid lineItemId,
            [FromBody] UpdateOrderLineItemRequest req,
            IOrderRepository repo, CancellationToken ct) =>
        {
            var order = await repo.GetByIdAsync(id, ct);
            if (order is null) return Results.NotFound();
            order.UpdateLineItem(lineItemId, req.Quantity, req.UnitPrice);
            await repo.UpdateAsync(order, ct);
            return Results.Ok(new { order.Id, order.TotalAmount });
        });

        group.MapDelete("/{id:guid}/line-items/{lineItemId:guid}", async (
            Guid id,
            Guid lineItemId,
            IOrderRepository repo, CancellationToken ct) =>
        {
            var order = await repo.GetByIdAsync(id, ct);
            if (order is null) return Results.NotFound();
            order.RemoveLineItem(lineItemId);
            await repo.UpdateAsync(order, ct);
            return Results.Ok(new { order.Id, order.TotalAmount });
        });

        return app;
    }
}

record ShipOrderRequest(string? TrackingInfo);
record CancelOrderRequest(string Reason);
record AddOrderLineItemRequest(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);
record UpdateOrderLineItemRequest(int Quantity, decimal UnitPrice);
