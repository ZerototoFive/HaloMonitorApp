namespace HaloMonitor.DTO
{
    public class CpuDto
    {
        public string? CPUInfo { get; set; }
        public string? TotalLoad { get; set; }
        public string? CPUPackageTemp { get; set; }
        public string? CoreAverageTemp { get; set; }
        public string? CoreMaxTemp { get; set; }
        public string? CPUVoltage { get; set; }
        public string? Power { get; set; }

        public Dictionary<string, string> Clock { get; set; } = new();
    }
}
