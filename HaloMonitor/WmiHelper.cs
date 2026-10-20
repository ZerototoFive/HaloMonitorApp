using System.Management;
using System.Net.NetworkInformation;

namespace HaloMonitor
{
    internal static class WmiHelper
    {
        // ================= 内存静态元数据 =================
        public record MemorySummary(
            string FormattedString,
            double TotalGB,
            double Speed,
            string Type,
            string Slots);

        private static MemorySummary? _cachedMemorySummary;

        public static MemorySummary GetMemorySummary()
        {
            if (_cachedMemorySummary != null)
                return _cachedMemorySummary;

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "root\\CIMV2",
                    "SELECT Capacity, ConfiguredClockSpeed, Speed, SMBIOSMemoryType, Manufacturer, PartNumber FROM Win32_PhysicalMemory");

                ulong totalBytes = 0;
                uint maxSpeed = 0;
                uint smbiosType = 0;
                int moduleCount = 0;
                ulong singleModuleBytes = 0;

                foreach (ManagementObject obj in searcher.Get())
                {
                    moduleCount++;
                    if (obj["Capacity"] != null && ulong.TryParse(obj["Capacity"].ToString(), out ulong cap))
                    {
                        totalBytes += cap;
                        if (singleModuleBytes == 0)
                            singleModuleBytes = cap;
                    }

                    if (obj["ConfiguredClockSpeed"] != null && uint.TryParse(obj["ConfiguredClockSpeed"].ToString(), out uint ccs) && ccs > 0)
                    {
                        if (ccs > maxSpeed) maxSpeed = ccs;
                    }
                    else if (obj["Speed"] != null && uint.TryParse(obj["Speed"].ToString(), out uint spd) && spd > 0)
                    {
                        if (spd > maxSpeed) maxSpeed = spd;
                    }

                    if (obj["SMBIOSMemoryType"] != null && uint.TryParse(obj["SMBIOSMemoryType"].ToString(), out uint smb) && smb > 0)
                    {
                        smbiosType = smb;
                    }
                }

                if (moduleCount > 0)
                {
                    double totalGB = Math.Round((double)totalBytes / (1024 * 1024 * 1024), 2);
                    double singleGB = Math.Round((double)singleModuleBytes / (1024 * 1024 * 1024), 0);
                    string type = smbiosType switch
                    {
                        20 => "DDR",
                        21 => "DDR2",
                        24 => "DDR3",
                        26 => "DDR4",
                        30 or 34 => "DDR5",
                        _ => "DDR"
                    };

                    string slots = $"{moduleCount} x {singleGB} GB";
                    string formatted = maxSpeed > 0
                        ? $"{totalGB} GB {type} @ {maxSpeed} MHz ({slots})"
                        : $"{totalGB} GB {type} ({slots})";

                    _cachedMemorySummary = new MemorySummary(formatted, totalGB, maxSpeed, type, slots);
                    return _cachedMemorySummary;
                }
            }
            catch
            {
                // swallow
            }

            _cachedMemorySummary = new MemorySummary("Generic Memory", 0, 0, "RAM", "");
            return _cachedMemorySummary;
        }

        // ================= 硬盘型号与挂载盘符元数据 =================
        public record DiskDriveMeta(string Model, string Partitions);

        private static Dictionary<int, DiskDriveMeta>? _cachedDiskMap;

        public static DiskDriveMeta GetDiskDriveInfo(int diskIndex)
        {
            if (_cachedDiskMap == null)
            {
                var map = new Dictionary<int, DiskDriveMeta>();
                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        "root\\CIMV2",
                        "SELECT Index, Model, DeviceID FROM Win32_DiskDrive");

                    foreach (ManagementObject drive in searcher.Get())
                    {
                        if (drive["Index"] == null || !int.TryParse(drive["Index"].ToString(), out int idx))
                            continue;

                        string model = drive["Model"]?.ToString()?.Trim() ?? string.Empty;
                        string partitions = string.Empty;

                        try
                        {
                            // 查找此物理磁盘对应的所有分区与盘符
                            string driveId = drive["DeviceID"]?.ToString() ?? string.Empty;
                            if (!string.IsNullOrEmpty(driveId))
                            {
                                string partQuery = $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{driveId}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition";
                                using var partSearcher = new ManagementObjectSearcher(partQuery);
                                var letters = new List<string>();

                                foreach (ManagementObject part in partSearcher.Get())
                                {
                                    string partId = part["DeviceID"]?.ToString() ?? string.Empty;
                                    string logQuery = $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partId}'}} WHERE AssocClass=Win32_LogicalDiskToPartition";
                                    using var logSearcher = new ManagementObjectSearcher(logQuery);
                                    foreach (ManagementObject log in logSearcher.Get())
                                    {
                                        var letter = log["DeviceID"]?.ToString();
                                        if (!string.IsNullOrEmpty(letter))
                                            letters.Add(letter);
                                    }
                                }

                                if (letters.Count > 0)
                                    partitions = string.Join(", ", letters);
                            }
                        }
                        catch { }

                        map[idx] = new DiskDriveMeta(model, partitions);
                    }
                }
                catch
                {
                    // swallow
                }

                _cachedDiskMap = map;
            }

            if (_cachedDiskMap.TryGetValue(diskIndex, out var meta))
                return meta;

            // 备选兜底：返回空
            return new DiskDriveMeta(string.Empty, string.Empty);
        }

        // ================= 网络设备扩展信息 =================
        public record NetworkAdapterMeta(string Description, string IPv4, string LinkSpeed, string MacAddress);

        public static NetworkAdapterMeta GetNetworkAdapterDetails(string hardwareName)
        {
            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces();
                NetworkInterface? target = null;

                // 1. 精确名称匹配
                foreach (var nic in nics)
                {
                    if (string.Equals(nic.Name, hardwareName, StringComparison.OrdinalIgnoreCase))
                    {
                        target = nic;
                        break;
                    }
                }

                // 2. 兜底模糊匹配（排除回环）
                if (target == null)
                {
                    foreach (var nic in nics)
                    {
                        if (nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                        {
                            target = nic;
                            break;
                        }
                    }
                }

                if (target != null)
                {
                    string ipv4 = string.Empty;
                    var ipProps = target.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            ipv4 = addr.Address.ToString();
                            break;
                        }
                    }

                    string linkSpeed = target.Speed > 0
                        ? $"{Math.Round((double)target.Speed / 1_000_000, 0)} Mbps"
                        : string.Empty;

                    string mac = string.Join(":", target.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));

                    return new NetworkAdapterMeta(
                        target.Description,
                        ipv4,
                        linkSpeed,
                        mac
                    );
                }
            }
            catch
            {
                // swallow
            }

            return new NetworkAdapterMeta(string.Empty, string.Empty, string.Empty, string.Empty);
        }

        // ================= 主板与 BIOS 元数据 =================
        public record MotherboardSummary(string Manufacturer, string Product, string BiosVersion);

        private static MotherboardSummary? _cachedMbSummary;

        public static MotherboardSummary GetMotherboardDetails()
        {
            if (_cachedMbSummary != null)
                return _cachedMbSummary;

            string mfg = string.Empty;
            string product = string.Empty;
            string bios = string.Empty;

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "root\\CIMV2",
                    "SELECT Manufacturer, Product, Version FROM Win32_BaseBoard");

                foreach (ManagementObject obj in searcher.Get())
                {
                    mfg = obj["Manufacturer"]?.ToString()?.Trim() ?? string.Empty;
                    product = obj["Product"]?.ToString()?.Trim() ?? string.Empty;
                    bios = obj["Version"]?.ToString()?.Trim() ?? string.Empty;
                    break;
                }
            }
            catch { }

            _cachedMbSummary = new MotherboardSummary(mfg, product, bios);
            return _cachedMbSummary;
        }

        // ================= CPU 核心数与线程数 =================
        public static (int PhysicalCores, int LogicalThreads) GetCpuCoreInfo()
        {
            int cores = 0;
            int threads = Environment.ProcessorCount;

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "root\\CIMV2",
                    "SELECT NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");

                foreach (ManagementObject obj in searcher.Get())
                {
                    if (obj["NumberOfCores"] != null && int.TryParse(obj["NumberOfCores"].ToString(), out int c))
                        cores += c;
                    if (obj["NumberOfLogicalProcessors"] != null && int.TryParse(obj["NumberOfLogicalProcessors"].ToString(), out int t))
                        threads = t;
                }
            }
            catch { }

            return (cores > 0 ? cores : threads, threads);
        }
    }
}
