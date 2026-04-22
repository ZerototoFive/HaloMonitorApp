using HaloMonitor.DTO;
using LibreHardwareMonitor.Hardware;

namespace HaloMonitor
{
    public class HardwareMonitor
    {
        private readonly Computer _computer;
        private readonly PerformanceCounterHelper _pcHelper;

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
            _pcHelper = new PerformanceCounterHelper();
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
                UpdateHardware(hw);

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

                    case HardwareType.GpuAmd:
                    case HardwareType.GpuNvidia:
                    case HardwareType.GpuIntel:
                        envelope.Body.Add(new Dictionary<string, object>
                        {
                            ["GPU"] = MapGpu(hw)
                        });
                        break;

                    case HardwareType.Network:
                        var net = MapNetwork(hw);
                        if (net != null && net.DataDownloaded != "0.00 GB")
                            networks.Add(net);
                        break;

                    case HardwareType.Storage:
                        var storage = MapStorage(hw);
                        if (storage != null)
                        {
                            envelope.Body.Add(new Dictionary<string, object>
                            {
                                ["Storage"] = storage
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

        // ================= 递归 Update =================
        private void UpdateHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (var sub in hardware.SubHardware)
                UpdateHardware(sub);
        }

        // ================= CPU =================
        private CpuDto MapCpu(IHardware hw)
        {
            var dto = new CpuDto
            {
                CPUInfo = hw.Name
            };

            // CPU Load（优先 PerformanceCounter）
            try
            {
                dto.TotalLoad = _pcHelper.GetCpuUsage().ToString("F1");
            }
            catch { }

            ReadCpuRecursive(hw, dto);

            // ===== fallback =====
            if (dto.CPUPackageTemp == null)
            {
                var t = WmiHelper.GetCpuTemperature();
                if (t != null)
                    dto.CPUPackageTemp = t.Value.ToString("F1");
            }

            if (dto.CPUVoltage == null)
            {
                var v = WmiHelper.GetCpuVoltage();
                if (v != null)
                    dto.CPUVoltage = v.Value.ToString("F3");
            }

            if (dto.Power == null || dto.Power == "0.00")
            {
                var p = WmiHelper.GetCpuPower();
                if (p != null)
                    dto.Power = p.Value.ToString("F2");
            }

            return dto;
        }

        private void ReadCpuRecursive(IHardware hw, CpuDto dto)
        {
            foreach (var s in hw.Sensors)
            {
                if (s.Value == null) continue;

                var v = s.Value.Value;

                switch (s.SensorType)
                {
                    case SensorType.Temperature:
                        if (dto.CPUPackageTemp == null &&
                            (s.Name.Contains("Package") || s.Name.Contains("Tctl") || s.Name.Contains("Die")))
                        {
                            dto.CPUPackageTemp = v.ToString("F1");
                        }
                        break;

                    case SensorType.Voltage:
                        if (dto.CPUVoltage == null && s.Name.Contains("Core"))
                            dto.CPUVoltage = v.ToString("F3");
                        break;

                    case SensorType.Power:
                        if (dto.Power == null && s.Name.Contains("CPU"))
                            dto.Power = v.ToString("F2");
                        break;

                    case SensorType.Clock:
                        dto.Clock[s.Name.Replace(" ", "").Replace("#", "")] =
                            v.ToString("F1");
                        break;
                }
            }

            foreach (var sub in hw.SubHardware)
                ReadCpuRecursive(sub, dto);
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
                    dto.MemoryLoad ??= v.ToString("F1");

                if (s.SensorType == SensorType.Data && s.Name.Contains("Used"))
                    dto.Used ??= v.ToString("F2");

                if (s.SensorType == SensorType.Data && s.Name.Contains("Available"))
                    dto.Free ??= v.ToString("F2");
            }

            return dto;
        }

        // ================= GPU（修复 FanLoad） =================
        private GpuDto MapGpu(IHardware hw)
        {
            var dto = new GpuDto { GPUInfo = hw.Name };

            foreach (var s in hw.Sensors)
            {
                if (s.Value == null) continue;
                var v = s.Value.Value;

                if (s.SensorType == SensorType.Load && s.Name.Contains("Core"))
                    dto.GPULoad ??= v.ToString("F1");

                if (s.SensorType == SensorType.Load && s.Name.Contains("Memory"))
                    dto.GPUMemoryLoad ??= v.ToString("F1");

                if (s.SensorType == SensorType.Clock && s.Name.Contains("Core"))
                    dto.GPUCoreClock ??= v.ToString("F1");

                if (s.SensorType == SensorType.Clock && s.Name.Contains("Memory"))
                    dto.GPUMemoryClock ??= v.ToString("F1");

                if (s.SensorType == SensorType.Temperature)
                    dto.Temp ??= v.ToString("F1");

                if (s.SensorType == SensorType.Fan)
                    dto.FanSpeed ??= v;

                if (s.SensorType == SensorType.Control && s.Name.Contains("Fan"))
                    dto.FanLoad ??= v;
            }

            // fallback：用转速估算 FanLoad
            if (dto.FanLoad == null && dto.FanSpeed != null)
            {
                dto.FanLoad = Math.Min(100, dto.FanSpeed.Value / 30.0);
            }

            return dto;
        }

        // ================= Network =================
        private NetworkDto? MapNetwork(IHardware hw)
        {
            var dto = new NetworkDto { NetworkInfo = hw.Name };
            bool hasData = false;

            foreach (var s in hw.Sensors)
            {
                if (s.Value == null) continue;

                if (s.SensorType == SensorType.Throughput && s.Name.Contains("Download"))
                {
                    dto.DownloadSpeed = FormatSpeed(s.Value.Value);
                    hasData = true;
                }

                if (s.SensorType == SensorType.Throughput && s.Name.Contains("Upload"))
                {
                    dto.UploadSpeed = FormatSpeed(s.Value.Value);
                    hasData = true;
                }

                if (s.SensorType == SensorType.Data && s.Name.Contains("Downloaded"))
                {
                    dto.DataDownloaded = FormatGB(s.Value.Value);
                    hasData = true;
                }
            }

            return hasData ? dto : null;
        }

        // ================= Storage（过滤垃圾数据） =================
        private StorageDto? MapStorage(IHardware hw)
        {
            var dto = new StorageDto
            {
                DiskInfo = hw.Name
            };

            bool hasValidData = false;

            foreach (var s in hw.Sensors)
            {
                if (s.Value == null) continue;

                var v = s.Value.Value;

                if (s.SensorType == SensorType.Temperature)
                    dto.Temp ??= v.ToString("F1");

                if (s.SensorType == SensorType.Load && s.Name.Contains("Used"))
                {
                    dto.UsedPercent = v.ToString("F1");
                    hasValidData = true;
                }

                if (s.SensorType == SensorType.Data && s.Name.Contains("Read"))
                {
                    dto.Read = FormatGB(v);
                    hasValidData = true;
                }

                if (s.SensorType == SensorType.Data && s.Name.Contains("Written"))
                {
                    dto.Written = FormatGB(v);
                    hasValidData = true;
                }

                if (s.SensorType == SensorType.Throughput && s.Name.Contains("Read"))
                {
                    dto.ReadSpeed = FormatSpeed(v);
                    hasValidData = true;
                }

                if (s.SensorType == SensorType.Throughput && s.Name.Contains("Write"))
                {
                    dto.WriteSpeed = FormatSpeed(v);
                    hasValidData = true;
                }
            }

            return hasValidData ? dto : null;
        }

        private static string FormatSpeed(double bytesPerSec)
        {
            var kb = bytesPerSec / 1024.0;
            return kb < 1024
                ? $"{kb:F1} Kb/s"
                : $"{(kb / 1024):F1} Mb/s";
        }

        private string FormatGB(double value)
        {
            return value >= 1024
                ? $"{value / 1024:F2} TB"
                : $"{value:F2} GB";
        }
    }
}