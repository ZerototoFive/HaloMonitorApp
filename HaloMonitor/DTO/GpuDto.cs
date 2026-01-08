
namespace HaloMonitor.DTO
{
    internal class GpuDto
    {
        public string? GPUInfo { get; set; }
        public string? GPULoad { get; set; }
        public string? GPUMemoryLoad { get; set; }
        public string? GPUCoreClock { get; set; }
        public string? GPUMemoryClock { get; set; }
        public string? Temp { get; set; }
        public string? HotSpotTemp { get; set; }

        public double? FanSpeed { get; set; }
        public double? FanLoad { get; set; }
        public double? GPUMemoryTotal{ get; set; }
        public double? GPUMemoryFree{ get; set; }
        public double? GPUMemoryUsed{ get; set; }
    }
}
