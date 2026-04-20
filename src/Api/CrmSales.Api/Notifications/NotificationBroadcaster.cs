using System.Collections.Concurrent;
using System.Threading.Channels;

namespace CrmSales.Api.Notifications;

public sealed class NotificationBroadcaster : INotificationBroadcaster
{
    private readonly ConcurrentDictionary<string, (string TenantId, Channel<NotificationEvent> Channel)> _clients = new();

    public (string ChannelId, ChannelReader<NotificationEvent> Reader) Subscribe(string tenantId)
    {
        var channelId = Guid.NewGuid().ToString();
        var channel = Channel.CreateUnbounded<NotificationEvent>(
            new UnboundedChannelOptions { SingleReader = true });
        _clients[channelId] = (tenantId, channel);
        return (channelId, channel.Reader);
    }

    public void Unsubscribe(string channelId)
    {
        if (_clients.TryRemove(channelId, out var entry))
            entry.Channel.Writer.TryComplete();
    }

    public async Task BroadcastAsync(NotificationEvent evt, CancellationToken ct = default)
    {
        foreach (var (_, (tenantId, channel)) in _clients)
        {
            if (tenantId == evt.TenantId)
                await channel.Writer.WriteAsync(evt, ct);
        }
    }
}
