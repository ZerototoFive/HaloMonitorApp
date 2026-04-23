using HaloMonitor.DTO;
using LibreHardwareMonitor.Hardware;

namespace HaloMonitor
{
    public class HardwareMonitor
    {
        private readonly Computer _computer;

        public HardwareMonitor()
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsNetworkEnabled = true,
                IsStorageEnabled = true,
            };

            _computer.Open();
        }

        public MonitorEnvelopeDto Read()
        {
            var envelope = new MonitorEnvelopeDto
            {
                Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            var networks = new List<NetworkDto>();

            foreach (var hw in _computer.Hardware)
            {
                Update(hw);

                switch (hw.HardwareType)
                {
                    case HardwareType.Cpu:
                        envelope.Body.Add(new Dictionary<string, object>
                        {
                            ["CPU"] = MapCpu(hw)
                        });
                        break;

                    case HardwareType.Memory:
                        if (!hw.Name.Contains("Total")) break;

                        envelope.Body.Add(new Dictionary<string, object>
                        {
                            ["Memory"] = MapMemory(hw)
                        });
                        break;

                    case HardwareType.GpuNvidia:
                    case HardwareType.GpuAmd:
                    case HardwareType.GpuIntel:
                        envelope.Body.Add(new Dictionary<string, object>
                        {
                            ["GPU"] = MapGpu(hw)
                        });
                        break;

                    case HardwareType.Network:
                        var net = MapNetwork(hw);
                        if (net != null)
                            networks.Add(net);
                        break;

                    case HardwareType.Storage:
                        var disk = MapStorage(hw);
                        if (disk != null)
                        {
                            envelope.Body.Add(new Dictionary<string, object>
                            {
                                ["Storage"] = disk
                            });
                        }
                        break;
                }
            }

            for (int i = 0; i < networks.Count; i++)
            {
                envelope.Body.Add(new Dictionary<string, object>
                {
                    [$"Network{i}"] = networks[i]
                });
            }

            return envelope;
        }

        private void Update(IHardware hw)
        {
            hw.Update();
            foreach (var sub in hw.SubHardware)
                Update(sub);
        }

        private static double Round0(double v) => Math.Round(v, 0, MidpointRounding.AwayFromZero);
        private static double Round2(double v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

        // ================= CPU（已增强） =================
        private CpuDto MapCpu(IHardware hw)
        {
            var dto = new CpuDto { CPUInfo = hw.Name };

            foreach (var s in hw.Sensors)
            {
                if (s.Value == null) continue;
                var v = s.Value.Value;

                // ===== Load =====
                if (s.SensorType == SensorType.Load && s.Name.Contains("Total"))
                    dto.TotalLoad = Round0(v);

                // ===== Temp =====
                if (s.SensorType == SensorType.Temperature && s.Name.Contains("Package"))
                    dto.CPUPackageTemp ??= Round0(v);

                if (s.SensorType == SensorType.Temperature && s.Name.Contains("Core Average"))
                    dto.CoreAverageTemp ??= Round0(v);

                if (s.SensorType == SensorType.Temperature && s.Name.Contains("Core Max"))
                    dto.CoreMaxTemp ??= Round0(v);

                // ===== Voltage =====
                if (s.SensorType == SensorType.Voltage && s.Name.Contains("Core"))
                    dto.CPUVoltage ??= Round2(v);

                // ===== Power =====
                if (s.SensorType == SensorType.Power && s.Name.Contains("CPU"))
                    dto.Power ??= Round0(v);

                // ===== Clock（关键修复）=====
                if (s.SensorType == SensorType.Clock)
                {
                    var name = s.Name;

                    // 兼容 Intel 12代 / AMD / 旧CPU
                    if (name.Contains("Core") || name.Contains("CPU") || name.Contains("Bus"))
                    {
                        var key = name
                            .Replace(" ", "")
                            .Replace("#", "")
                            .Replace("Core", "Core")
                            .Replace("P-Core", "PCore")
                            .Replace("E-Core", "ECore");

                        dto.Clock[key] = Round2(v);
                    }
                }
            }

            return dto;
        }

        // ================= Memory =================
        private MemoryDto MapMemory(IHardware hw)
        {
            var dto = new MemoryDto
            {
                MemoryInfo = "Generic Memory"
            };

            foreach (var s in hw.Sensors)
            {
                if (s.Value == null) continue;
                var v = s.Value.Value;

                if (s.SensorType == SensorType.Load)
                    dto.MemoryLoad = Round2(v);

                if (s.SensorType == SensorType.Data && s.Name.Contains("Used"))
                    dto.Used = Round2(v);

                if (s.SensorType == SensorType.Data && s.Name.Contains("Available"))
                    dto.Free = Round2(v);
            }

            return dto;
        }

        // ================= GPU =================
        private GpuDto MapGpu(IHardware hw)
        {
            var dto = new GpuDto { GPUInfo = hw.Name };

            foreach (var s in hw.Sensors)
            {
                if (s.Value == null) continue;
                var v = s.Value.Value;

                if (s.SensorType == SensorType.Load && s.Name.Contains("Core"))
                    dto.GPULoad = Round0(v);

                if (s.SensorType == SensorType.Load && s.Name.Contains("Memory"))
                    dto.GPUMemoryLoad = Round2(v);

                if (s.SensorType == SensorType.Clock && s.Name.Contains("Core"))
                    dto.GPUCoreClock = Round0(v);

                if (s.SensorType == SensorType.Clock && s.Name.Contains("Memory"))
                    dto.GPUMemoryClock = Round0(v);

                if (s.SensorType == SensorType.Temperature)
                    dto.Temp ??= Round0(v);

                if (s.SensorType == SensorType.Temperature && s.Name.Contains("Hot Spot"))
                    dto.HotSpotTemp ??= Round0(v);

                if (s.SensorType == SensorType.Fan)
                    dto.FanSpeed ??= Round0(v);

                if (s.SensorType == SensorType.Control)
                    dto.FanLoad ??= Round0(v);

                if (s.SensorType == SensorType.SmallData && s.Name.Contains("Total"))
                    dto.GPUMemoryTotal ??= Round0(v);

                if (s.SensorType == SensorType.SmallData && s.Name.Contains("Free"))
                    dto.GPUMemoryFree ??= Round0(v);

                if (s.SensorType == SensorType.SmallData && s.Name.Contains("Used"))
                    dto.GPUMemoryUsed ??= Round0(v);
            }

            return dto;
        }

        // ================= Network（🔥过滤优化） =================
        private NetworkDto? MapNetwork(IHardware hw)
        {
            // ❌ 过滤虚拟网卡
            if (hw.Name.Contains("本地连接*") || hw.Name.Contains("Local"))
                return null;

            var dto = new NetworkDto { NetworkInfo = hw.Name };
            bool hasTraffic = false;

            double downSpeed = 0;
            double upSpeed = 0;
            double downloaded = 0;
            double uploaded = 0;

            foreach (var s in hw.Sensors)
            {
                if (s.Value == null) continue;
                var v = s.Value.Value;

                if (s.SensorType == SensorType.Throughput && s.Name.Contains("Download"))
                {
                    downSpeed = Round2(v / 1024.0);
                }

                if (s.SensorType == SensorType.Throughput && s.Name.Contains("Upload"))
                {
                    upSpeed = Round2(v / 1024.0);
                }

                if (s.SensorType == SensorType.Data && s.Name.Contains("Downloaded"))
                {
                    downloaded = Round2(v);
                }

                if (s.SensorType == SensorType.Data && s.Name.Contains("Uploaded"))
                {
                    uploaded = Round2(v);
                }
            }

            // ✅ 关键过滤逻辑
            if (downSpeed > 0 || upSpeed > 0 || downloaded > 0 || uploaded > 0)
            {
                hasTraffic = true;
            }

            if (!hasTraffic)
                return null;

            dto.DownloadSpeed = downSpeed;
            dto.UploadSpeed = upSpeed;
            dto.DataDownloaded = downloaded;
            dto.DataUploaded = uploaded;

            return dto;
        }

        // ================= Storage =================
        private StorageDto? MapStorage(IHardware hw)
        {
            var dto = new StorageDto { DiskInfo = hw.Name };
            bool has = false;

            foreach (var s in hw.Sensors)
            {
                if (s.Value == null) continue;
                var v = s.Value.Value;

                if (s.SensorType == SensorType.Temperature)
                    dto.Temp ??= Round0(v);

                if (s.SensorType == SensorType.Load && s.Name.Contains("Used"))
                {
                    dto.UsedPercent = Round2(v);
                    has = true;
                }

                if (s.SensorType == SensorType.Data && s.Name.Contains("Read"))
                {
                    dto.Read = Round0(v);
                    has = true;
                }

                if (s.SensorType == SensorType.Data && s.Name.Contains("Written"))
                {
                    dto.Written = Round0(v);
                    has = true;
                }

                if (s.SensorType == SensorType.Throughput && s.Name.Contains("Read"))
                {
                    dto.ReadSpeed = Round0(v / 1024.0);
                    has = true;
                }

                if (s.SensorType == SensorType.Throughput && s.Name.Contains("Write"))
                {
                    dto.WriteSpeed = Round0(v / 1024.0);
                    has = true;
                }
            }

            return has ? dto : null;
        }
    }
}