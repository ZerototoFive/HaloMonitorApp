namespace HaloMonitor.DTO
{
    public class NetworkDto
    {
        public string? NetworkInfo { get; set; }

        public double? DataDownloaded { get; set; } // GB
        public double? DataUploaded { get; set; }

        public double? DownloadSpeed { get; set; } // KB/s
        public double? UploadSpeed { get; set; }
    }
}