
namespace HaloMonitor.DTO
{
    internal class StorageDto
    {
        public string DiskInfo { get; set; } = string.Empty;

        public string? Temp { get; set; }

        public string? UsedPercent { get; set; }

        public string? Read { get; set; }

        public string? Written { get; set; }

        public string? ReadSpeed { get; set; }

        public string? WriteSpeed { get; set; }
    }
}
