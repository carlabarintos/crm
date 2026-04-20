using CrmSales.SharedKernel.MultiTenancy;
using CrmSales.Api.Notifications;
using Microsoft.AspNetCore.Http.Features;
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

            // Disable ASP.NET Core response buffering so events reach the client immediately
            http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

            var (channelId, reader) = broadcaster.Subscribe(tenant.TenantId);
            try
            {
                // Confirm connection is open
                await http.Response.WriteAsync(": connected\n\n", ct);
                await http.Response.Body.FlushAsync(ct);

                while (!ct.IsCancellationRequested)
                {
                    // Race: wait for an event OR send a heartbeat every 15 s to keep the connection alive
                    var waitForData = reader.WaitToReadAsync(ct).AsTask();
                    var heartbeat = Task.Delay(15_000, ct);

                    var completed = await Task.WhenAny(waitForData, heartbeat);

                    if (ct.IsCancellationRequested) break;

                    if (completed == waitForData)
                    {
                        // Drain all pending events
                        while (reader.TryRead(out var evt))
                        {
                            var json = JsonSerializer.Serialize(evt);
                            await http.Response.WriteAsync($"data: {json}\n\n", ct);
                        }
                    }
                    else
                    {
                        // Heartbeat — keeps proxies and the client from treating the connection as dead
                        await http.Response.WriteAsync(": heartbeat\n\n", ct);
                    }

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
