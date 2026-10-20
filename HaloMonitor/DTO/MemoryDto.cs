namespace HaloMonitor.DTO
{
    public class MemoryDto
    {
        public string? MemoryInfo { get; set; }

        public double? MemoryLoad { get; set; }
        public double? Used { get; set; }   // 物理内存已用 (GB)
        public double? Free { get; set; }   // 物理内存可用 (GB)
        public double? Total { get; set; }  // 物理内存总量 (GB)

        public double? Speed { get; set; }  // 内存运行频率 (MHz)
        public string? Type { get; set; }   // 内存代际 (DDR4 / DDR5)
        public string? Slots { get; set; }  // 内存插槽配置 (如 2 x 16 GB)

        public double? VirtualUsed { get; set; }  // 虚拟内存/分页文件已用 (GB)
        public double? VirtualFree { get; set; }  // 虚拟内存/分页文件可用 (GB)
        public double? VirtualTotal { get; set; } // 虚拟内存总量 (GB)
    }
}
