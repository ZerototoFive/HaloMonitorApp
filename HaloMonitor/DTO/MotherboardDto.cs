namespace HaloMonitor.DTO
{
    public class MotherboardDto
    {
        public string? MotherboardInfo { get; set; }
        public Dictionary<string, string> Fans { get; set; } = new();
        public Dictionary<string, string> FansLoads { get; set; } = new();
    }
}
