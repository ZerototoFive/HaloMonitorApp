namespace HaloMonitor.DTO
{
    public class NetworkDto
    {
        public string? NetworkInfo { get; set; }
        public string? AdapterName { get; set; }    // 网卡硬件芯片/描述 (如 Intel I225-V)
        public string? IPv4 { get; set; }           // 内网 IPv4 地址
        public string? LinkSpeed { get; set; }      // 连接协商速率 (如 1000 Mbps)
        public string? MacAddress { get; set; }     // MAC 地址

        public double? DataDownloaded { get; set; } // 累计下载量 (GB)
        public double? DataUploaded { get; set; }   // 累计上传量 (GB)

        public double? DownloadSpeed { get; set; }  // 下载实时速度 (KB/s)
        public double? UploadSpeed { get; set; }    // 上传实时速度 (KB/s)
    }
}
