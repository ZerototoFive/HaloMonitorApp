namespace HaloMonitor.DTO
{
    public class CpuDto
    {
        public string? CPUInfo { get; set; }

        public double? TotalLoad { get; set; }
        public double? CoreMaxLoad { get; set; }     // 最大核心占用率 (%)
        public double? CPUPackageTemp { get; set; }
        public double? CoreAverageTemp { get; set; }
        public double? CoreMaxTemp { get; set; }
        public double? CPUVoltage { get; set; }
        public double? Power { get; set; }          // CPU Package 功耗 (W)
        public double? CorePower { get; set; }      // CPU 核心总功耗 (W)

        public int? Cores { get; set; }             // 物理核心数
        public int? Threads { get; set; }           // 逻辑核心/线程数

        public Dictionary<string, double> Clock { get; set; } = new();
        public Dictionary<string, double> CoreTemperatures { get; set; } = new();
        public Dictionary<string, double> CoreLoads { get; set; } = new();        // 每个物理核心占用率 (%)
        public Dictionary<string, double> ThreadLoads { get; set; } = new();      // 每个逻辑线程占用率 (%)
    }
}
