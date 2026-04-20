using System.Threading.Channels;

namespace CrmSales.Api.Notifications;

public interface INotificationBroadcaster
{
    (string ChannelId, ChannelReader<NotificationEvent> Reader) Subscribe(string tenantId);
    void Unsubscribe(string channelId);
    Task BroadcastAsync(NotificationEvent evt, CancellationToken ct = default);
}
