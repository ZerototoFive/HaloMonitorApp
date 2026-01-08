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
                hw.Update();

                switch (hw.HardwareType)
                {
                    case HardwareType.Cpu:
                        envelope.Body.Add(new Dictionary<string, object>
                        {
                            ["CPU"] = MapCpu(hw)
                        });
                        break;

                    case HardwareType.Memory:
                        envelope.Body.Add(new Dictionary<string, object>
                        {
                            ["memory"] = MapMemory(hw)
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
                        if (net != null && !net.DataDownloaded.Equals("0.00 GB"))
                            networks.Add(net);
                        break;
                    case HardwareType.Storage:
                        envelope.Body.Add(new Dictionary<string, object>
                        {
                            ["Storage"] = MapStorage(hw)
                        });
                        break;
                }
            }

            // Network 按顺序追加 Network0 / Network1 ...
            for (int i = 0; i < networks.Count; i++)
            {
                envelope.Body.Add(new Dictionary<string, object>
                {
                    [$"Network{i}"] = networks[i]
                });
            }

            return envelope;
        }

        // ================= CPU =================

        private CpuDto MapCpu(IHardware hw)
        {
            var dto = new CpuDto
            {
                CPUInfo = hw.Name
            };

            foreach (var s in hw.Sensors)
            {
                if (s.Value == null) continue;

                var v = s.Value.Value;

                switch (s.SensorType)
                {
                    case SensorType.Load when s.Name == "CPU Total":
                        dto.TotalLoad = v.ToString("F1");
                        break;

                    case SensorType.Temperature when s.Name == "CPU Package":
                        dto.CPUPackageTemp = v.ToString("F1");
                        break;

                    case SensorType.Temperature when s.Name == "Core Average":
                        dto.CoreAverageTemp = v.ToString("F1");
                        break;

                    case SensorType.Temperature when s.Name == "Core Max":
                        dto.CoreMaxTemp = v.ToString("F1");
                        break;

                    case SensorType.Voltage when s.Name == "CPU Core":
                        dto.CPUVoltage = v.ToString("F3");
                        break;

                    case SensorType.Power when s.Name == "CPU Cores":
                        dto.Power = v.ToString("F2");
                        break;

                    case SensorType.Clock when s.Name.Contains("CPU"):
                        dto.Clock[s.Name.Replace(" ", "").Replace("#", "")] =
                            v.ToString("F1");
                        break;
                }
            }

            return dto;
        }

        // ================= MEMORY =================

        private MemoryDto MapMemory(IHardware hw)
        {
            var dto = new MemoryDto
            {
                MemoryInfo = hw.Name
            };

            foreach (var s in hw.Sensors)
            {
                if (s.Value == null) continue;

                var v = s.Value.Value;

                if (s.SensorType == SensorType.Load && s.Name == "Memory")
                    dto.MemoryLoad = v.ToString("F1");

                if (s.SensorType == SensorType.Data && s.Name == "Memory Used")
                    dto.Used = v.ToString("F2");

                if (s.SensorType == SensorType.Data && s.Name == "Memory Available")
                    dto.Free = v.ToString("F2");
            }

            return dto;
        }

        // ================= GPU =================

        private GpuDto MapGpu(IHardware hw)
        {
            var dto = new GpuDto
            {
                GPUInfo = hw.Name
            };

            foreach (var s in hw.Sensors)
            {
                if (s.Value == null) continue;

                var v = s.Value.Value;

                switch (s.SensorType)
                {
                    case SensorType.Load when s.Name == "GPU Core":
                        dto.GPULoad = v.ToString("F1");
                        break;

                    case SensorType.Load when s.Name == "GPU Memory":
                        dto.GPUMemoryLoad = v.ToString("F1");
                        break;

                    case SensorType.Clock when s.Name == "GPU Core":
                        dto.GPUCoreClock = v.ToString("F1");
                        break;

                    case SensorType.Clock when s.Name == "GPU Memory":
                        dto.GPUMemoryClock = v.ToString("F1");
                        break;
                         
                    case SensorType.Temperature when s.Name == "GPU Core":
                        dto.Temp = v.ToString("F1");
                        break;

                    case SensorType.Temperature when s.Name == "GPU Hot Spot":
                        dto.HotSpotTemp = v.ToString("F1");
                        break;

                    case SensorType.Fan when s.Name == "GPU":
                        dto.FanSpeed = v;
                        break;

                    case SensorType.Control when s.Name == "GPU Fan":
                        dto.FanLoad = v;
                        break;

                    case SensorType.SmallData when s.Name == "GPU Memory Total":
                        dto.GPUMemoryTotal = v;
                        break;

                    case SensorType.SmallData when s.Name == "GPU Memory Free":
                        dto.GPUMemoryFree = v;
                        break;

                    case SensorType.SmallData when s.Name == "GPU Memory Used":
                        dto.GPUMemoryUsed = v;
                        break;

                }
            }

            return dto;
        }

        // ================= NETWORK =================

        private NetworkDto? MapNetwork(IHardware hw)
        {
            var dto = new NetworkDto
            {
                NetworkInfo = hw.Name
            };

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

                if (s.SensorType == SensorType.Data && s.Name.Contains("Uploaded"))
                {
                    dto.DataUploaded = FormatGB(s.Value.Value);
                    hasData = true;
                }
            }

            return hasData ? dto : null;
        }

        //=============== Storage ===================
        private StorageDto MapStorage(IHardware hw)
        {
            var dto = new StorageDto
            {
                DiskInfo = hw.Name
            };

            foreach (var s in hw.Sensors)
            {
                if (s.Value == null) continue;

                var v = s.Value.Value;

                switch (s.SensorType)
                {
                    case SensorType.Temperature:
                        dto.Temp = v.ToString("F1");
                        break;

                    case SensorType.Load when s.Name.Contains("Used Space"):
                        dto.UsedPercent = v.ToString("F1");
                        break;

                    case SensorType.Data when s.Name.Contains("Read"):
                        dto.Read = FormatGB(v);
                        break;

                    case SensorType.Data when s.Name.Contains("Written"):
                        dto.Written = FormatGB(v);
                        break;

                    case SensorType.Throughput when s.Name.Contains("Read"):
                        dto.ReadSpeed = FormatSpeed(v);
                        break;

                    case SensorType.Throughput when s.Name.Contains("Write"):
                        dto.WriteSpeed = FormatSpeed(v);
                        break;
                }
            }

            return dto;
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
