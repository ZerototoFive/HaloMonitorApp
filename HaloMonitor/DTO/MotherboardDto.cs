namespace HaloMonitor.DTO
{
    public class MotherboardDto
    {
        public string? MotherboardInfo { get; set; }
        public string? Manufacturer { get; set; }
        public string? BiosVersion { get; set; }

        public Dictionary<string, double> Fans { get; set; } = new();
        public Dictionary<string, double> FansLoads { get; set; } = new();
        public Dictionary<string, double> Temperatures { get; set; } = new();
        public Dictionary<string, double> Voltages { get; set; } = new();
    }
}
