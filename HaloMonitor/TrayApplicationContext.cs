using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HaloMonitor
{
    public sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        private readonly CancellationTokenSource _cts = new();

        public TrayApplicationContext()
        {
            _trayIcon = new NotifyIcon
            {
                Text = "Halo Monitor",
                Icon = LoadIcon(),
                Visible = true,
                ContextMenuStrip = BuildMenu()
            };

            StartBackgroundAsync();
        }

        private static Icon LoadIcon()
        {
            // 从当前 exe 中提取嵌入的 ApplicationIcon
            var exePath = Application.ExecutablePath;
            return Icon.ExtractAssociatedIcon(exePath)
                   ?? SystemIcons.Application;
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("退出", null, (_, _) => Exit());
            return menu;
        }

        private void Exit()
        {
            _cts.Cancel();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            ExitThread();
        }

        private void StartBackgroundAsync()
        {
            Task.Run(async () =>
            {
                try
                {
                    var services = new ServiceCollection();

                    // === 配置 ===
                    var configuration = new ConfigurationBuilder()
                        .SetBasePath(AppContext.BaseDirectory)
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                        .Build();

                    services.Configure<MonitorConfig>(
                        configuration.GetSection("Monitor"));

                    // === 核心服务 ===
                    services.AddSingleton<HardwareMonitor>();
                    services.AddSingleton<MonitorWebSocketClient>();

                    // === 日志（可选，先不输出到 console） ===
                    services.AddLogging(b =>
                    {
                        b.SetMinimumLevel(LogLevel.Information);
                    });

                    using var sp = services.BuildServiceProvider();

                    var client = sp.GetRequiredService<MonitorWebSocketClient>();

                    await client.RunAsync(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // 正常退出
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        ex.Message,
                        "Halo Monitor Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }, _cts.Token);
        }
    }
}
