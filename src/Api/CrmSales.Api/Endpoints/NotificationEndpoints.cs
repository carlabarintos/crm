using CrmSales.SharedKernel.MultiTenancy;
using CrmSales.Api.Notifications;
using System.Text.Json;

namespace CrmSales.Api.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/notifications/stream", async (
            HttpContext http,
            INotificationBroadcaster broadcaster,
            ITenantContext tenant,
            CancellationToken ct) =>
        {
            http.Response.ContentType = "text/event-stream";
            http.Response.Headers.CacheControl = "no-cache";
            http.Response.Headers.Connection = "keep-alive";
            http.Response.Headers["X-Accel-Buffering"] = "no";

            var (channelId, reader) = broadcaster.Subscribe(tenant.TenantId);
            try
            {
                // Send a heartbeat comment immediately to confirm the stream is open
                await http.Response.WriteAsync(": connected\n\n", ct);
                await http.Response.Body.FlushAsync(ct);

                await foreach (var evt in reader.ReadAllAsync(ct))
                {
                    var json = JsonSerializer.Serialize(evt);
                    await http.Response.WriteAsync($"data: {json}\n\n", ct);
                    await http.Response.Body.FlushAsync(ct);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                broadcaster.Unsubscribe(channelId);
            }
        }).RequireAuthorization();

        return app;
    }
}
