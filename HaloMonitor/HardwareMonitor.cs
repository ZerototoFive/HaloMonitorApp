using System.Runtime.CompilerServices;
using HaloMonitor.DTO;
using LibreHardwareMonitor.Hardware;

namespace HaloMonitor
{
    public class HardwareMonitor : IDisposable
    {
        private readonly Computer _computer;
        private bool _disposed;
        private bool _isInitialized;

        private readonly List<CpuReader> _cpuReaders = new();
        private readonly List<MemoryReader> _memoryReaders = new();
        private readonly List<GpuReader> _gpuReaders = new();
        private readonly List<StorageReader> _storageReaders = new();
        private readonly List<NetworkReader> _networkReaders = new();

        private int _readCount;
        private const int StorageUpdateInterval = 5; // 硬盘分频轮询：每5次循环更新一次，大幅压低磁盘IO/SMART查询开销

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

        private void EnsureInitialized()
        {
            if (_isInitialized) return;

            // 首次全量更新，使硬件与各传感器完成底层注册
            foreach (var hw in _computer.Hardware)
            {
                UpdateHardware(hw);
            }

            // 建立静态映射与传感器引用预绑定
            foreach (var hw in _computer.Hardware)
            {
                switch (hw.HardwareType)
                {
                    case HardwareType.Cpu:
                        _cpuReaders.Add(new CpuReader(hw));
                        break;

                    case HardwareType.Memory:
                        if (hw.Name.Contains("Total"))
                            _memoryReaders.Add(new MemoryReader(hw));
                        break;

                    case HardwareType.GpuNvidia:
                    case HardwareType.GpuAmd:
                    case HardwareType.GpuIntel:
                        _gpuReaders.Add(new GpuReader(hw));
                        break;

                    case HardwareType.Network:
                        // 预先过滤虚拟网卡
                        if (!hw.Name.Contains("本地连接*") && !hw.Name.Contains("Local"))
                        {
                            _networkReaders.Add(new NetworkReader(hw));
                        }
                        break;

                    case HardwareType.Storage:
                        _storageReaders.Add(new StorageReader(hw));
                        break;
                }
            }

            _isInitialized = true;
        }

        public MonitorEnvelopeDto Read()
        {
            EnsureInitialized();

            _readCount++;
            bool shouldUpdateStorage = (_readCount % StorageUpdateInterval == 0) || _readCount == 1;

            var envelope = new MonitorEnvelopeDto
            {
                Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            // 1. CPU
            foreach (var cpu in _cpuReaders)
            {
                cpu.Update();
                envelope.Body.Add(new Dictionary<string, object>(1)
                {
                    ["CPU"] = cpu.Read()
                });
            }

            // 2. Memory
            foreach (var mem in _memoryReaders)
            {
                mem.Update();
                envelope.Body.Add(new Dictionary<string, object>(1)
                {
                    ["Memory"] = mem.Read()
                });
            }

            // 3. GPU
            foreach (var gpu in _gpuReaders)
            {
                gpu.Update();
                envelope.Body.Add(new Dictionary<string, object>(1)
                {
                    ["GPU"] = gpu.Read()
                });
            }

            // 4. Storage (分频更新，显著降低高延迟IOCTL与内核切换)
            foreach (var disk in _storageReaders)
            {
                if (shouldUpdateStorage)
                {
                    disk.Update();
                }

                var diskDto = disk.Read(shouldUpdateStorage);
                if (diskDto != null)
                {
                    envelope.Body.Add(new Dictionary<string, object>(1)
                    {
                        ["Storage"] = diskDto
                    });
                }
            }

            // 5. Network (过滤无流量网卡后按 Network0, Network1... 编号)
            int netIndex = 0;
            foreach (var net in _networkReaders)
            {
                net.Update();
                var netDto = net.Read();
                if (netDto != null)
                {
                    envelope.Body.Add(new Dictionary<string, object>(1)
                    {
                        [$"Network{netIndex++}"] = netDto
                    });
                }
            }

            return envelope;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double Round0(double v) => Math.Round(v, 0, MidpointRounding.AwayFromZero);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double Round2(double v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

        private static void UpdateHardware(IHardware hw)
        {
            hw.Update();
            foreach (var sub in hw.SubHardware)
                UpdateHardware(sub);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                try
                {
                    _computer.Close();
                }
                catch
                {
                    // swallow
                }
                GC.SuppressFinalize(this);
            }
        }

        // ================= 各硬件专用 Reader（预计算与传感器绑定） =================

        private sealed class CpuReader
        {
            private readonly IHardware _hw;
            private readonly string _name;
            private readonly ISensor? _totalLoad;
            private readonly ISensor? _packageTemp;
            private readonly ISensor? _coreAvgTemp;
            private readonly ISensor? _coreMaxTemp;
            private readonly ISensor? _coreVoltage;
            private readonly ISensor? _power;
            private readonly (string Key, ISensor Sensor)[] _clocks;

            public CpuReader(IHardware hw)
            {
                _hw = hw;
                _name = hw.Name;

                var clocks = new List<(string Key, ISensor Sensor)>();

                foreach (var s in hw.Sensors)
                {
                    if (s.SensorType == SensorType.Load && s.Name.Contains("Total"))
                        _totalLoad ??= s;
                    else if (s.SensorType == SensorType.Temperature && s.Name.Contains("Package"))
                        _packageTemp ??= s;
                    else if (s.SensorType == SensorType.Temperature && s.Name.Contains("Core Average"))
                        _coreAvgTemp ??= s;
                    else if (s.SensorType == SensorType.Temperature && s.Name.Contains("Core Max"))
                        _coreMaxTemp ??= s;
                    else if (s.SensorType == SensorType.Voltage && s.Name.Contains("Core"))
                        _coreVoltage ??= s;
                    else if (s.SensorType == SensorType.Power && s.Name.Contains("CPU"))
                        _power ??= s;
                    else if (s.SensorType == SensorType.Clock)
                    {
                        var n = s.Name;
                        if (n.Contains("Core") || n.Contains("CPU") || n.Contains("Bus"))
                        {
                            var cleanKey = n
                                .Replace(" ", "")
                                .Replace("#", "")
                                .Replace("Core", "Core")
                                .Replace("P-Core", "PCore")
                                .Replace("E-Core", "ECore");
                            clocks.Add((cleanKey, s));
                        }
                    }
                }

                _clocks = clocks.ToArray();
            }

            public void Update() => UpdateHardware(_hw);

            public CpuDto Read()
            {
                var dto = new CpuDto
                {
                    CPUInfo = _name,
                    TotalLoad = _totalLoad?.Value != null ? Round0(_totalLoad.Value.Value) : null,
                    CPUPackageTemp = _packageTemp?.Value != null ? Round0(_packageTemp.Value.Value) : null,
                    CoreAverageTemp = _coreAvgTemp?.Value != null ? Round0(_coreAvgTemp.Value.Value) : null,
                    CoreMaxTemp = _coreMaxTemp?.Value != null ? Round0(_coreMaxTemp.Value.Value) : null,
                    CPUVoltage = _coreVoltage?.Value != null ? Round2(_coreVoltage.Value.Value) : null,
                    Power = _power?.Value != null ? Round0(_power.Value.Value) : null,
                };

                for (int i = 0; i < _clocks.Length; i++)
                {
                    var val = _clocks[i].Sensor.Value;
                    if (val.HasValue)
                    {
                        dto.Clock[_clocks[i].Key] = Round2(val.Value);
                    }
                }

                return dto;
            }
        }

        private sealed class MemoryReader
        {
            private readonly IHardware _hw;
            private readonly ISensor? _load;
            private readonly ISensor? _used;
            private readonly ISensor? _free;

            public MemoryReader(IHardware hw)
            {
                _hw = hw;
                foreach (var s in hw.Sensors)
                {
                    if (s.SensorType == SensorType.Load)
                        _load ??= s;
                    else if (s.SensorType == SensorType.Data && s.Name.Contains("Used"))
                        _used ??= s;
                    else if (s.SensorType == SensorType.Data && s.Name.Contains("Available"))
                        _free ??= s;
                }
            }

            public void Update() => UpdateHardware(_hw);

            public MemoryDto Read()
            {
                return new MemoryDto
                {
                    MemoryInfo = "Generic Memory",
                    MemoryLoad = _load?.Value != null ? Round2(_load.Value.Value) : null,
                    Used = _used?.Value != null ? Round2(_used.Value.Value) : null,
                    Free = _free?.Value != null ? Round2(_free.Value.Value) : null,
                };
            }
        }

        private sealed class GpuReader
        {
            private readonly IHardware _hw;
            private readonly string _name;
            private readonly ISensor? _coreLoad;
            private readonly ISensor? _memLoad;
            private readonly ISensor? _coreClock;
            private readonly ISensor? _memClock;
            private readonly ISensor? _temp;
            private readonly ISensor? _hotSpotTemp;
            private readonly ISensor? _fanSpeed;
            private readonly ISensor? _fanLoad;
            private readonly ISensor? _memTotal;
            private readonly ISensor? _memFree;
            private readonly ISensor? _memUsed;

            public GpuReader(IHardware hw)
            {
                _hw = hw;
                _name = hw.Name;

                foreach (var s in hw.Sensors)
                {
                    if (s.SensorType == SensorType.Load && s.Name.Contains("Core"))
                        _coreLoad ??= s;
                    else if (s.SensorType == SensorType.Load && s.Name.Contains("Memory"))
                        _memLoad ??= s;
                    else if (s.SensorType == SensorType.Clock && s.Name.Contains("Core"))
                        _coreClock ??= s;
                    else if (s.SensorType == SensorType.Clock && s.Name.Contains("Memory"))
                        _memClock ??= s;
                    else if (s.SensorType == SensorType.Temperature)
                    {
                        if (s.Name.Contains("Hot Spot"))
                            _hotSpotTemp ??= s;
                        else
                            _temp ??= s;
                    }
                    else if (s.SensorType == SensorType.Fan)
                        _fanSpeed ??= s;
                    else if (s.SensorType == SensorType.Control)
                        _fanLoad ??= s;
                    else if (s.SensorType == SensorType.SmallData)
                    {
                        if (s.Name.Contains("Total"))
                            _memTotal ??= s;
                        else if (s.Name.Contains("Free"))
                            _memFree ??= s;
                        else if (s.Name.Contains("Used"))
                            _memUsed ??= s;
                    }
                }
            }

            public void Update() => UpdateHardware(_hw);

            public GpuDto Read()
            {
                return new GpuDto
                {
                    GPUInfo = _name,
                    GPULoad = _coreLoad?.Value != null ? Round0(_coreLoad.Value.Value) : null,
                    GPUMemoryLoad = _memLoad?.Value != null ? Round2(_memLoad.Value.Value) : null,
                    GPUCoreClock = _coreClock?.Value != null ? Round0(_coreClock.Value.Value) : null,
                    GPUMemoryClock = _memClock?.Value != null ? Round0(_memClock.Value.Value) : null,
                    Temp = _temp?.Value != null ? Round0(_temp.Value.Value) : null,
                    HotSpotTemp = _hotSpotTemp?.Value != null ? Round0(_hotSpotTemp.Value.Value) : null,
                    FanSpeed = _fanSpeed?.Value != null ? Round0(_fanSpeed.Value.Value) : null,
                    FanLoad = _fanLoad?.Value != null ? Round0(_fanLoad.Value.Value) : null,
                    GPUMemoryTotal = _memTotal?.Value != null ? Round0(_memTotal.Value.Value) : null,
                    GPUMemoryFree = _memFree?.Value != null ? Round0(_memFree.Value.Value) : null,
                    GPUMemoryUsed = _memUsed?.Value != null ? Round0(_memUsed.Value.Value) : null,
                };
            }
        }

        private sealed class NetworkReader
        {
            private readonly IHardware _hw;
            private readonly string _name;
            private readonly ISensor? _downSpeed;
            private readonly ISensor? _upSpeed;
            private readonly ISensor? _downloaded;
            private readonly ISensor? _uploaded;

            public NetworkReader(IHardware hw)
            {
                _hw = hw;
                _name = hw.Name;

                foreach (var s in hw.Sensors)
                {
                    if (s.SensorType == SensorType.Throughput)
                    {
                        if (s.Name.Contains("Download"))
                            _downSpeed ??= s;
                        else if (s.Name.Contains("Upload"))
                            _upSpeed ??= s;
                    }
                    else if (s.SensorType == SensorType.Data)
                    {
                        if (s.Name.Contains("Downloaded"))
                            _downloaded ??= s;
                        else if (s.Name.Contains("Uploaded"))
                            _uploaded ??= s;
                    }
                }
            }

            public void Update() => UpdateHardware(_hw);

            public NetworkDto? Read()
            {
                double downSpeed = _downSpeed?.Value != null ? Round2(_downSpeed.Value.Value / 1024.0) : 0;
                double upSpeed = _upSpeed?.Value != null ? Round2(_upSpeed.Value.Value / 1024.0) : 0;
                double downloaded = _downloaded?.Value != null ? Round2(_downloaded.Value.Value) : 0;
                double uploaded = _uploaded?.Value != null ? Round2(_uploaded.Value.Value) : 0;

                if (downSpeed == 0 && upSpeed == 0 && downloaded == 0 && uploaded == 0)
                    return null;

                return new NetworkDto
                {
                    NetworkInfo = _name,
                    DownloadSpeed = downSpeed,
                    UploadSpeed = upSpeed,
                    DataDownloaded = downloaded,
                    DataUploaded = uploaded
                };
            }
        }

        private sealed class StorageReader
        {
            private readonly IHardware _hw;
            private readonly string _name;
            private readonly ISensor? _temp;
            private readonly ISensor? _usedPercent;
            private readonly ISensor? _read;
            private readonly ISensor? _written;
            private readonly ISensor? _readSpeed;
            private readonly ISensor? _writeSpeed;
            private StorageDto? _cachedDto;

            public StorageReader(IHardware hw)
            {
                _hw = hw;
                _name = hw.Name;

                foreach (var s in hw.Sensors)
                {
                    if (s.SensorType == SensorType.Temperature)
                        _temp ??= s;
                    else if (s.SensorType == SensorType.Load && s.Name.Contains("Used"))
                        _usedPercent ??= s;
                    else if (s.SensorType == SensorType.Data)
                    {
                        if (s.Name.Contains("Read"))
                            _read ??= s;
                        else if (s.Name.Contains("Written"))
                            _written ??= s;
                    }
                    else if (s.SensorType == SensorType.Throughput)
                    {
                        if (s.Name.Contains("Read"))
                            _readSpeed ??= s;
                        else if (s.Name.Contains("Write"))
                            _writeSpeed ??= s;
                    }
                }
            }

            public void Update() => UpdateHardware(_hw);

            public StorageDto? Read(bool forceRecompute)
            {
                if (!forceRecompute && _cachedDto != null)
                    return _cachedDto;

                bool has = false;
                var dto = new StorageDto { DiskInfo = _name };

                if (_temp?.Value != null)
                    dto.Temp = Round0(_temp.Value.Value);

                if (_usedPercent?.Value != null)
                {
                    dto.UsedPercent = Round2(_usedPercent.Value.Value);
                    has = true;
                }

                if (_read?.Value != null)
                {
                    dto.Read = Round0(_read.Value.Value);
                    has = true;
                }

                if (_written?.Value != null)
                {
                    dto.Written = Round0(_written.Value.Value);
                    has = true;
                }

                if (_readSpeed?.Value != null)
                {
                    dto.ReadSpeed = Round0(_readSpeed.Value.Value / 1024.0);
                    has = true;
                }

                if (_writeSpeed?.Value != null)
                {
                    dto.WriteSpeed = Round0(_writeSpeed.Value.Value / 1024.0);
                    has = true;
                }

                _cachedDto = has ? dto : null;
                return _cachedDto;
            }
        }
    }
}