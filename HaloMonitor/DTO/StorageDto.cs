namespace HaloMonitor.DTO
{
    public class StorageDto
    {
        public string DiskInfo { get; set; } = string.Empty;
        public string? Partitions { get; set; }    // 挂载逻辑盘符，如 "C:, D:"

        public double? Temp { get; set; }
        public double? UsedPercent { get; set; }

        public double? TotalSpace { get; set; }    // 磁盘总容量 (GB)
        public double? FreeSpace { get; set; }     // 磁盘剩余容量 (GB)
        public double? UsedSpace { get; set; }     // 磁盘已用容量 (GB)
        public double? Health { get; set; }        // 寿命/健康度百分比 (0-100)

        public double? Read { get; set; }          // 累计读取量 (GB)
        public double? Written { get; set; }       // 累计写入量 (GB)

        public double? ReadSpeed { get; set; }     // 读速率 (KB/s)
        public double? WriteSpeed { get; set; }    // 写速率 (KB/s)
    }
}
