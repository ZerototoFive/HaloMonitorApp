namespace HaloMonitor
{
    public sealed class MonitorConfig
    {
        public string DeviceId { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public int ReconnectSeconds { get; set; } = 5;
        public int ReportIntervalSeconds { get; set; } = 1;
    }

}