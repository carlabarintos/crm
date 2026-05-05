using CrmSales.Api.Auditing;
using CrmSales.SharedKernel.Catalog;
using CrmSales.SharedKernel.MultiTenancy;
using CrmSales.Api.Notifications;
using CrmSales.Contacts.Domain.Repositories;
using CrmSales.Orders.Application.Services;
using CrmSales.Orders.Domain.Entities;
using CrmSales.Orders.Domain.Repositories;
using CrmSales.Products.Domain.Repositories;
using CrmSales.Settings.Application.Services;
using CrmSales.Settings.Domain.Entities;
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
                Status = o.Status.ToString(),
                o.SubTotal, o.DiscountTotal, o.TotalAmount,
                o.QuoteDiscountPercent, o.QuoteDiscountAmount, o.TaxableAmount,
                o.TaxAmount, o.GrandTotal,
                o.Currency, o.Notes, o.ShippingAddress,
                LineItems = o.LineItems.Select(l => new
                {
                    l.Id, l.ItemName, l.Quantity, l.UnitPrice, l.DiscountPercent, l.LineTotal, l.DiscountAmount
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
                order.SubTotal, order.DiscountTotal, order.TotalAmount,
                order.QuoteDiscountPercent, order.QuoteDiscountAmount, order.TaxableAmount,
                order.TaxRateName, order.TaxRatePercent,
                order.TaxAmount, order.GrandTotal, order.Currency,
                order.ShippingAddress, order.Notes,
                LineItems = order.LineItems.Select(l => new
                {
                    l.Id, l.CatalogItemId, l.ItemName, ItemType = l.ItemType.ToString(),
                    l.Quantity, l.UnitPrice, l.DiscountPercent, l.LineTotal, l.DiscountAmount
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
                order.SubTotal, order.DiscountTotal, order.TotalAmount,
                order.QuoteDiscountPercent, order.QuoteDiscountAmount, order.TaxableAmount,
                order.TaxRateName, order.TaxRatePercent,
                order.TaxAmount, order.GrandTotal, order.Currency,
                order.ShippingAddress, order.Notes,
                LineItems = order.LineItems.Select(l => new
                {
                    l.Id, l.CatalogItemId, l.ItemName, ItemType = l.ItemType.ToString(),
                    l.Quantity, l.UnitPrice, l.DiscountPercent, l.LineTotal, l.DiscountAmount
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

            foreach (var item in order.LineItems.Where(l => l.CatalogItemId.HasValue))
            {
                var product = await productRepo.GetByIdAsync(item.CatalogItemId!.Value, ct);
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
            if (req.CatalogItemId == null && string.IsNullOrWhiteSpace(req.ItemName))
                return Results.Problem("Item name is required for custom line items.", statusCode: 400);
            var itemType = Enum.TryParse<CatalogItemType>(req.ItemType, true, out var t) ? t : CatalogItemType.Product;
            order.AddLineItem(req.CatalogItemId, req.ItemName, req.Quantity, req.UnitPrice, itemType);
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

        // ── Tax ───────────────────────────────────────────────────────────────
        group.MapPost("/{id:guid}/tax", async (
            Guid id,
            [FromBody] ApplyOrderTaxRequest req,
            IOrderRepository repo, CancellationToken ct) =>
        {
            var order = await repo.GetByIdAsync(id, ct);
            if (order is null) return Results.NotFound();
            order.ApplyTax(req.TaxRateName, req.TaxRatePercent);
            await repo.UpdateAsync(order, ct);
            return Results.Ok(new { order.TaxRateName, order.TaxRatePercent, order.TaxableAmount, order.TaxAmount, order.GrandTotal });
        });

        group.MapDelete("/{id:guid}/tax", async (
            Guid id,
            IOrderRepository repo, CancellationToken ct) =>
        {
            var order = await repo.GetByIdAsync(id, ct);
            if (order is null) return Results.NotFound();
            order.RemoveTax();
            await repo.UpdateAsync(order, ct);
            return Results.Ok(new { order.TaxRateName, order.TaxRatePercent, order.TaxableAmount, order.TaxAmount, order.GrandTotal });
        });

        // ── Discount ──────────────────────────────────────────────────────────
        group.MapPut("/{id:guid}/discount", async (
            Guid id,
            [FromBody] SetOrderDiscountRequest req,
            IOrderRepository repo, CancellationToken ct) =>
        {
            var order = await repo.GetByIdAsync(id, ct);
            if (order is null) return Results.NotFound();
            order.SetQuoteDiscount(req.Percent);
            await repo.UpdateAsync(order, ct);
            return Results.Ok(new { order.QuoteDiscountPercent, order.QuoteDiscountAmount, order.TaxableAmount, order.TaxAmount, order.GrandTotal });
        });

        group.MapDelete("/{id:guid}/discount", async (
            Guid id,
            IOrderRepository repo, CancellationToken ct) =>
        {
            var order = await repo.GetByIdAsync(id, ct);
            if (order is null) return Results.NotFound();
            order.RemoveQuoteDiscount();
            await repo.UpdateAsync(order, ct);
            return Results.Ok(new { order.QuoteDiscountPercent, order.QuoteDiscountAmount, order.TaxableAmount, order.TaxAmount, order.GrandTotal });
        });

        // ── Order Documents ────────────────────────────────────────────────────
        var docs = group.MapGroup("/{id:guid}/documents");

        docs.MapPost("/", async (
            Guid id,
            IFormFile file,
            IOrderRepository repo,
            IOrderDocumentStorage storage,
            IVirusScanService virusScanner,
            IStorageSettingsRepository settingsRepo,
            CancellationToken ct,
            [FromQuery] OrderDocumentType type = OrderDocumentType.Other,
            [FromQuery] string? notes = null) =>
        {
            var settings = await settingsRepo.GetAsync(ct);
            var maxFileSize = settings?.MaxFileSizeBytes ?? StorageSettings.DefaultMaxFileSizeBytes;
            var maxFiles = settings?.MaxFilesPerOrder ?? StorageSettings.DefaultMaxFilesPerOrder;

            if (file.Length > maxFileSize)
                return Results.Problem($"File size exceeds limit of {maxFileSize / (1024 * 1024)} MB.", statusCode: 400);

            string[] allowedTypes = ["image/jpeg", "image/png", "image/webp", "application/pdf"];
            if (!allowedTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
                return Results.Problem("File type not allowed. Accepted: JPEG, PNG, WebP, PDF.", statusCode: 400);

            var order = await repo.GetByIdAsync(id, ct);
            if (order is null) return Results.NotFound();

            if (order.Documents.Count >= maxFiles)
                return Results.Problem($"Order already has the maximum of {maxFiles} documents.", statusCode: 400);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            ms.Position = 0;

            var scanResult = await virusScanner.ScanAsync(ms, ct);
            if (scanResult is ScanResult.InfectedResult infected)
                return Results.Problem($"File rejected: virus detected ({infected.VirusName}).", statusCode: 400);
            if (scanResult is ScanResult.ErrorResult)
                return Results.Problem("File could not be scanned. Upload rejected.", statusCode: 503);

            ms.Position = 0;
            var key = await storage.UploadAsync(id, file.FileName, ms, file.ContentType, ct);
            var doc = OrderDocument.Create(id, type, file.FileName, key, file.ContentType, file.Length, notes);
            order.AttachDocument(doc);
            await repo.UpdateAsync(order, ct);

            return Results.Created($"/api/orders/{id}/documents/{doc.Id}", new
            {
                doc.Id, doc.FileName, Type = doc.Type.ToString(), doc.FileSizeBytes, doc.UploadedAt
            });
        }).DisableAntiforgery()
          .WithMetadata(new RequestSizeLimitAttribute(30_000_000)); // 30 MB — overrides the 512 KB global Kestrel limit for file uploads

        docs.MapGet("/", async (Guid id, IOrderRepository repo, CancellationToken ct) =>
        {
            var order = await repo.GetByIdAsync(id, ct);
            if (order is null) return Results.NotFound();
            return Results.Ok(order.Documents.Select(d => new
            {
                d.Id, d.FileName, Type = d.Type.ToString(), d.ContentType, d.FileSizeBytes, d.Notes, d.UploadedAt
            }));
        });

        docs.MapGet("/{docId:guid}/download", async (
            Guid id, Guid docId,
            IOrderRepository repo,
            IOrderDocumentStorage storage,
            CancellationToken ct) =>
        {
            var order = await repo.GetByIdAsync(id, ct);
            var doc = order?.Documents.FirstOrDefault(d => d.Id == docId);
            if (doc is null) return Results.NotFound();

            var stream = await storage.DownloadAsync(doc.StorageKey, ct);
            return Results.Stream(stream, doc.ContentType, doc.FileName);
        });

        docs.MapDelete("/{docId:guid}", async (
            Guid id, Guid docId,
            IOrderRepository repo,
            IOrderDocumentStorage storage,
            CancellationToken ct) =>
        {
            var order = await repo.GetByIdAsync(id, ct);
            if (order is null) return Results.NotFound();

            var removed = order.RemoveDocument(docId);
            if (removed is null) return Results.NotFound();

            await storage.DeleteAsync(removed.StorageKey, ct);
            await repo.UpdateAsync(order, ct);
            return Results.NoContent();
        });

        return app;
    }
}

record ShipOrderRequest(string? TrackingInfo);
record CancelOrderRequest(string Reason);
record AddOrderLineItemRequest(Guid? CatalogItemId, string ItemName, int Quantity, decimal UnitPrice, string ItemType = "Product");
record UpdateOrderLineItemRequest(int Quantity, decimal UnitPrice);
record ApplyOrderTaxRequest(string TaxRateName, decimal TaxRatePercent);
record SetOrderDiscountRequest(decimal Percent);
