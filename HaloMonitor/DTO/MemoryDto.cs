namespace HaloMonitor.DTO
{
    public class MemoryDto
    {
        public string? MemoryInfo { get; set; }

        public double? MemoryLoad { get; set; }
        public double? Used { get; set; }   // GB
        public double? Free { get; set; }   // GB
    }
}