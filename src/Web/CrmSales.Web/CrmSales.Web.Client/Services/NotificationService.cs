using System.Text.Json;

namespace CrmSales.Web.Client.Services;

public sealed class NotificationService(CrmApiClient api)
{
    public event Action<NotificationEventDto>? OnNotification;

    private readonly List<NotificationEventDto> _recent = new();
    public IReadOnlyList<NotificationEventDto> Recent => _recent;
    public int UnreadCount { get; private set; }

    private static readonly JsonSerializerOptions _json =
        new(JsonSerializerDefaults.Web);

    public void MarkAllRead() => UnreadCount = 0;

    public async Task StartAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ConnectAndReadAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch
            {
                // Wait before reconnecting to avoid hammering the server
                await Task.Delay(5000, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task ConnectAndReadAsync(CancellationToken ct)
    {
        using var response = await api.GetNotificationStreamAsync(ct);
        if (!response.IsSuccessStatusCode) return;

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break; // server closed the stream
            if (!line.StartsWith("data: ")) continue;

            var json = line[6..];
            var evt = JsonSerializer.Deserialize<NotificationEventDto>(json, _json);
            if (evt is null) continue;

            _recent.Insert(0, evt);
            if (_recent.Count > 50) _recent.RemoveAt(50);
            UnreadCount++;
            OnNotification?.Invoke(evt);
        }
    }
}
