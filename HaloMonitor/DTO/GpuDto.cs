namespace HaloMonitor.DTO
{
    public class GpuDto
    {
        public string? GPUInfo { get; set; }

        public double? GPULoad { get; set; }
        public double? GPUMemoryLoad { get; set; }

        public double? GPUCoreClock { get; set; }
        public double? GPUMemoryClock { get; set; }

        public double? Temp { get; set; }
        public double? HotSpotTemp { get; set; }

        public double? FanSpeed { get; set; }
        public double? FanLoad { get; set; }

        public double? GPUMemoryTotal { get; set; }
        public double? GPUMemoryFree { get; set; }
        public double? GPUMemoryUsed { get; set; }
    }
}