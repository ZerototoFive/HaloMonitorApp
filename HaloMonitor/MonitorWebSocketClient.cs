using HaloMonitor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
public sealed class MonitorWebSocketClient : IAsyncDisposable
{
    private readonly string _deviceId;
    private readonly Uri _serverUri;
    private readonly TimeSpan _reconnectDelay;
    private readonly TimeSpan _reportInterval;
    private readonly HardwareMonitor _monitor;
    private readonly ILogger<MonitorWebSocketClient> _logger;
    private ClientWebSocket? _ws;

    public MonitorWebSocketClient(
        IOptions<MonitorConfig> options,
        HardwareMonitor monitor,
        ILogger<MonitorWebSocketClient> logger)
    {
        var cfg = options.Value;

        _deviceId = cfg.DeviceId;
        _serverUri = new Uri(cfg.Url);
        _reconnectDelay = TimeSpan.FromSeconds(cfg.ReconnectSeconds);
        _reportInterval = TimeSpan.FromSeconds(cfg.ReportIntervalSeconds);

        _monitor = monitor;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Connecting to WebSocket: {Url}", _serverUri);
                await ConnectAndSendAsync(token);
                _logger.LogInformation("WebSocket connected");

            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("WebSocket send failed: {Message}", ex.Message);
            }

            await Task.Delay(_reconnectDelay, token);
        }
    }

    private async Task ConnectAndSendAsync(CancellationToken token)
    {
        _ws = new ClientWebSocket();
        _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        try
        {
            await _ws.ConnectAsync(_serverUri, token);

            while (_ws.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                var payload = _monitor.Read();
                payload.DeviceId = _deviceId;

                var json = JsonSerializer.Serialize(payload);
                var buffer = Encoding.UTF8.GetBytes(json);

                await _ws.SendAsync(
                    buffer,
                    WebSocketMessageType.Text,
                    true,
                    token
                );

                await Task.Delay(_reportInterval, token);
            }
        }
        finally
        {
            // 即使异常，也尽量关闭并释放
            if (_ws != null)
            {
                try
                {
                    if (_ws.State == WebSocketState.Open)
                        await _ws.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "cleanup",
                            CancellationToken.None);
                }
                catch { /* swallow */ }

                _ws.Dispose();
                _ws = null;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_ws != null)
        {
            if (_ws.State == WebSocketState.Open)
            {
                await _ws.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Service stopping",
                    CancellationToken.None);
            }

            _ws.Dispose();
        }
    }
}