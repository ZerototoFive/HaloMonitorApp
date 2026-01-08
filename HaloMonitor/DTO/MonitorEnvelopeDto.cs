namespace HaloMonitor.DTO
{
    public class MonitorEnvelopeDto
    {
        public string DeviceId { get; set; } = string.Empty;
        public string? Time { get; set; }

        public List<Dictionary<string, object>> Body { get; set; }
            = new();
    }
}
