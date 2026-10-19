using HaloMonitor;
using Microsoft.Extensions.Options;
using System.Net.WebSockets;
using System.Text.Json;

public sealed class MonitorWebSocketClient : IAsyncDisposable
{
    private readonly string _deviceId;
    private readonly Uri _serverUri;
    private readonly TimeSpan _reconnectDelay;
    private readonly TimeSpan _reportInterval;
    private readonly HardwareMonitor _monitor;
    private ClientWebSocket? _ws;

    public MonitorWebSocketClient(
        IOptions<MonitorConfig> options,
        HardwareMonitor monitor)
    {
        var cfg = options.Value;

        _deviceId = cfg.DeviceId;
        _serverUri = new Uri(cfg.Url);
        _reconnectDelay = TimeSpan.FromSeconds(cfg.ReconnectDelaySeconds);
        _reportInterval = TimeSpan.FromSeconds(cfg.ReportIntervalSeconds);

        _monitor = monitor;
    }

    public async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await ConnectAndSendAsync(token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // 日志记录已关闭，静默重试
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

                byte[] buffer = JsonSerializer.SerializeToUtf8Bytes(payload);

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