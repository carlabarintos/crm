using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.JSInterop;
using System.Text.Json;

namespace CrmSales.Web.Client.Services;

public sealed class NotificationService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly IAccessTokenProvider _tokenProvider;
    private readonly string _apiBase;

    private readonly CancellationTokenSource _cts = new();
    private DotNetObjectReference<NotificationService>? _selfRef;
    private bool _started;

    public event Action<NotificationEventDto>? OnNotification;

    private readonly List<NotificationEventDto> _recent = new();
    public IReadOnlyList<NotificationEventDto> Recent => _recent;
    public int UnreadCount { get; private set; }

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public NotificationService(IJSRuntime js, IAccessTokenProvider tokenProvider, CrmApiClient api)
    {
        _js = js;
        _tokenProvider = tokenProvider;
        _apiBase = api.BaseAddress?.ToString().TrimEnd('/') ?? "";
    }

    public void MarkAllRead() => UnreadCount = 0;

    // Called once by MainLayout when the user is authenticated.
    // Safe to call multiple times — only starts on first call.
    public async Task StartAsync()
    {
        if (_started) return;
        _started = true;
        await ConnectAsync();
    }

    private async Task ConnectAsync()
    {
        if (_cts.IsCancellationRequested) return;

        try
        {
            var tokenResult = await _tokenProvider.RequestAccessToken();
            if (!tokenResult.TryGetToken(out var token))
                return;

            var url = $"{_apiBase}/api/notifications/stream?access_token={Uri.EscapeDataString(token.Value)}";

            _selfRef?.Dispose();
            _selfRef = DotNetObjectReference.Create(this);
            await _js.InvokeVoidAsync("notifSse.start", _cts.Token, url, _selfRef);
        }
        catch (OperationCanceledException) { }
        catch
        {
            // JS runtime not ready yet — retry after a short delay
            await Task.Delay(2000, _cts.Token).ConfigureAwait(false);
            await ConnectAsync();
        }
    }

    [JSInvokable]
    public void OnSseMessage(string data)
    {
        var evt = JsonSerializer.Deserialize<NotificationEventDto>(data, _json);
        if (evt is null) return;

        _recent.Insert(0, evt);
        if (_recent.Count > 50) _recent.RemoveAt(50);
        UnreadCount++;
        OnNotification?.Invoke(evt);
    }

    [JSInvokable]
    public async Task OnSseClosed()
    {
        if (_cts.IsCancellationRequested) return;

        await Task.Delay(5000, _cts.Token).ConfigureAwait(false);
        await ConnectAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        try { await _js.InvokeVoidAsync("notifSse.stop"); } catch { }
        _selfRef?.Dispose();
        _cts.Dispose();
    }
}
