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
        public double? MemoryTemp { get; set; }      // 显存/Junction 温度

        public double? Power { get; set; }          // 显卡整卡/芯片功耗 (W)
        public double? GPUVoltage { get; set; }     // 显卡核心电压 (V)

        public double? FanSpeed { get; set; }       // 主风扇转速 (RPM)
        public double? FanLoad { get; set; }        // 主风扇占空比 (%)
        public Dictionary<string, double> Fans { get; set; } = new(); // 多风扇转速列表

        public double? GPUMemoryTotal { get; set; }
        public double? GPUMemoryFree { get; set; }
        public double? GPUMemoryUsed { get; set; }

        public double? VideoEngineLoad { get; set; } // 视频编解码引擎占用率 (%)
    }
}
