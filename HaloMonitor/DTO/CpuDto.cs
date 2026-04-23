namespace HaloMonitor.DTO
{
    public class CpuDto
    {
        public string? CPUInfo { get; set; }

        public double? TotalLoad { get; set; }
        public double? CPUPackageTemp { get; set; }
        public double? CoreAverageTemp { get; set; }
        public double? CoreMaxTemp { get; set; }
        public double? CPUVoltage { get; set; }
        public double? Power { get; set; }

        public Dictionary<string, double> Clock { get; set; } = new();
    }
}