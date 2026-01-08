using System;
using System.Collections.Generic;

namespace HaloMonitor.DTO
{
    internal class NetworkDto
    {
        public string? NetworkInfo { get; set; }
        public string? DataDownloaded { get; set; }
        public string? DataUploaded { get; set; }
        public string? DownloadSpeed { get; set; }
        public string? UploadSpeed { get; set; }
    }
}
