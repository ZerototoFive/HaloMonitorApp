namespace HaloMonitor.DTO
{
    public class StorageDto
    {
        public string DiskInfo { get; set; } = string.Empty;

        public double? Temp { get; set; }
        public double? UsedPercent { get; set; }

        public double? Read { get; set; }     // GB / TB 统一数值（单位前端处理）
        public double? Written { get; set; }

        public double? ReadSpeed { get; set; }   // KB/s
        public double? WriteSpeed { get; set; }
    }
}