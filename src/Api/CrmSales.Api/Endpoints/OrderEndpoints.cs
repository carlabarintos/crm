using CrmSales.Api.Auditing;
using CrmSales.SharedKernel.MultiTenancy;
using CrmSales.Api.Notifications;
using CrmSales.Contacts.Domain.Repositories;
using CrmSales.Orders.Domain.Entities;
using CrmSales.Orders.Domain.Repositories;
using CrmSales.Products.Domain.Repositories;
using CrmSales.Settings.Application.Services;
using CrmSales.Settings.Domain.Enums;
using CrmSales.Settings.Domain.Repositories;
using CrmSales.SharedKernel.Application;
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
            IOrderRepository repo,
            CancellationToken ct,
            [FromQuery] string? search = null,
            [FromQuery] OrderStatus? status = null,
            [FromQuery] int limit = 20,
            [FromQuery] string? cursor = null) =>
        {
            var result = await repo.SearchAsync(search, status, limit, cursor, ct);
            return Results.Ok(new
            {
                items = result.Items.Select(o => new
                {
                    o.Id, o.OrderNumber, o.QuoteId,
                    Status = o.Status.ToString(), o.TotalAmount, o.Currency,
                    o.CreatedAt, o.ShippedAt, o.DeliveredAt
                }),
                result.NextCursor,
                result.HasMore
            });
        });

        group.MapGet("/summary", async (IOrderRepository repo, CancellationToken ct) =>
        {
            var s = await repo.GetSummaryAsync(ct: ct);
            var monthly = s.MonthlyRevenue
                .ToDictionary(m => new DateTime(m.Year, m.Month, 1).ToString("MMM yy"), m => m.Revenue);
            return Results.Ok(new
            {
                totalCount      = s.Total,
                pendingCount    = s.Pending,
                activeCount     = s.Active,
                deliveredCount  = s.Delivered,
                cancelledCount  = s.Cancelled,
                deliveredRevenue = s.DeliveredRevenue,
                currency        = s.Currency,
                monthlyRevenue  = monthly
            });
        });

        group.MapGet("/customer/{customerId:guid}", async (Guid customerId, IOrderRepository repo, CancellationToken ct) =>
        {
            var orders = await repo.GetByCustomerAsync(customerId, ct);
            return Results.Ok(orders.Select(o => new
            {
                o.Id, o.OrderNumber, o.QuoteId,
                Status = o.Status.ToString(), o.TotalAmount, o.TaxAmount, o.GrandTotal,
                o.Currency, o.Notes, o.ShippingAddress,
                LineItems = o.LineItems.Select(l => new
                {
                    l.Id, l.ProductName, l.Quantity, l.UnitPrice, l.LineTotal
                }),
                o.CreatedAt, o.ShippedAt, o.DeliveredAt
            }));
        });

        group.MapGet("/by-quote/{quoteId:guid}", async (Guid quoteId, IOrderRepository repo, CancellationToken ct) =>
        {
            var order = await repo.GetByQuoteIdAsync(quoteId, ct);
            return order is null ? Results.NotFound() : Results.Ok(new
            {
                order.Id, order.OrderNumber, order.QuoteId,
                Status = order.Status.ToString(),
                order.TotalAmount, order.TaxRateName, order.TaxRatePercent,
                order.TaxAmount, order.GrandTotal, order.Currency,
                order.ShippingAddress, order.Notes,
                LineItems = order.LineItems.Select(l => new
                {
                    l.Id, l.ProductId, l.ProductName, l.Quantity, l.UnitPrice, l.LineTotal
                }),
                order.CreatedAt, order.ShippedAt, order.DeliveredAt
            });
        });

        group.MapGet("/{id:guid}", async (Guid id, IOrderRepository repo, CancellationToken ct) =>
        {
            var order = await repo.GetByIdAsync(id, ct);
            return order is null ? Results.NotFound() : Results.Ok(new
            {
                order.Id, order.OrderNumber, order.QuoteId,
                Status = order.Status.ToString(),
                order.TotalAmount, order.TaxRateName, order.TaxRatePercent,
                order.TaxAmount, order.GrandTotal, order.Currency,
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
            IContactRepository contactRepo,
            IEmailTemplateRepository emailTemplateRepo,
            IEmailService emailService,
            INotificationBroadcaster broadcaster,
            IAuditService audit,
            ITenantContext tenant,
            ILoggerFactory loggerFactory,
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

            if (order.CustomerId != Guid.Empty)
            {
                var contact = await contactRepo.GetByIdAsync(order.CustomerId, ct);
                if (contact?.Email is not null)
                {
                    var template = await emailTemplateRepo.GetByTypeAsync(EmailTemplateType.OrderConfirmed, ct);
                    if (template is { IsActive: true })
                    {
                        var vars = new Dictionary<string, string>
                        {
                            ["ContactName"] = contact.FullName,
                            ["OrderNumber"] = order.OrderNumber,
                            ["TotalAmount"] = order.GrandTotal.ToString("N2"),
                            ["Currency"] = order.Currency
                        };
                        try
                        {
                            await emailService.SendAsync(
                                contact.Email, contact.FullName,
                                TemplateRenderer.Render(template.Subject, vars),
                                TemplateRenderer.Render(template.BodyHtml, vars), ct);
                        }
                        catch (Exception ex)
                        {
                            loggerFactory.CreateLogger("OrderEndpoints").LogError(ex, "Failed to send order confirmation email for {OrderNumber}", order.OrderNumber);
                        }
                    }
                }
            }

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
            IContactRepository contactRepo,
            IEmailTemplateRepository emailTemplateRepo,
            IEmailService emailService,
            INotificationBroadcaster broadcaster,
            IAuditService audit,
            ITenantContext tenant,
            ILoggerFactory loggerFactory,
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

            if (order.CustomerId != Guid.Empty)
            {
                var contact = await contactRepo.GetByIdAsync(order.CustomerId, ct);
                if (contact?.Email is not null)
                {
                    var template = await emailTemplateRepo.GetByTypeAsync(EmailTemplateType.OrderShipped, ct);
                    if (template is { IsActive: true })
                    {
                        var vars = new Dictionary<string, string>
                        {
                            ["ContactName"] = contact.FullName,
                            ["OrderNumber"] = order.OrderNumber,
                            ["TotalAmount"] = order.GrandTotal.ToString("N2"),
                            ["Currency"] = order.Currency,
                            ["TrackingInfo"] = req.TrackingInfo ?? "N/A"
                        };
                        try
                        {
                            await emailService.SendAsync(
                                contact.Email, contact.FullName,
                                TemplateRenderer.Render(template.Subject, vars),
                                TemplateRenderer.Render(template.BodyHtml, vars), ct);
                        }
                        catch (Exception ex)
                        {
                            loggerFactory.CreateLogger("OrderEndpoints").LogError(ex, "Failed to send shipment email for {OrderNumber}", order.OrderNumber);
                        }
                    }
                }
            }

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
            IContactRepository contactRepo,
            IEmailTemplateRepository emailTemplateRepo,
            IEmailService emailService,
            INotificationBroadcaster broadcaster,
            IAuditService audit,
            ITenantContext tenant,
            ILoggerFactory loggerFactory,
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

            if (order.CustomerId != Guid.Empty)
            {
                var contact = await contactRepo.GetByIdAsync(order.CustomerId, ct);
                if (contact?.Email is not null)
                {
                    var template = await emailTemplateRepo.GetByTypeAsync(EmailTemplateType.OrderCancelled, ct);
                    if (template is { IsActive: true })
                    {
                        var vars = new Dictionary<string, string>
                        {
                            ["ContactName"] = contact.FullName,
                            ["OrderNumber"] = order.OrderNumber,
                            ["TotalAmount"] = order.GrandTotal.ToString("N2"),
                            ["Currency"] = order.Currency,
                            ["CancellationReason"] = req.Reason
                        };
                        try
                        {
                            await emailService.SendAsync(
                                contact.Email, contact.FullName,
                                TemplateRenderer.Render(template.Subject, vars),
                                TemplateRenderer.Render(template.BodyHtml, vars), ct);
                        }
                        catch (Exception ex)
                        {
                            loggerFactory.CreateLogger("OrderEndpoints").LogError(ex, "Failed to send cancellation email for {OrderNumber}", order.OrderNumber);
                        }
                    }
                }
            }

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
