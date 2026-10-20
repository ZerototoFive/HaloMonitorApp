using System.Linq;
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

        private readonly List<MotherboardReader> _motherboardReaders = new();
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
                IsMotherboardEnabled = true,
                IsControllerEnabled = true,
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

            IHardware? virtualMemHw = null;
            IHardware? totalMemHw = null;

            // 预检索内存组件
            foreach (var hw in _computer.Hardware)
            {
                if (hw.HardwareType == HardwareType.Memory)
                {
                    if (hw.Name.Contains("Total", StringComparison.OrdinalIgnoreCase))
                        totalMemHw = hw;
                    else if (hw.Name.Contains("Virtual", StringComparison.OrdinalIgnoreCase))
                        virtualMemHw = hw;
                }
            }

            // 建立静态映射与传感器引用预绑定
            int diskOrder = 0;
            foreach (var hw in _computer.Hardware)
            {
                switch (hw.HardwareType)
                {
                    case HardwareType.Motherboard:
                        _motherboardReaders.Add(new MotherboardReader(hw));
                        break;

                    case HardwareType.Cpu:
                        _cpuReaders.Add(new CpuReader(hw));
                        break;

                    case HardwareType.Memory:
                        if (hw == totalMemHw || (totalMemHw == null && hw.Name.Contains("Total")))
                        {
                            _memoryReaders.Add(new MemoryReader(hw, virtualMemHw));
                        }
                        break;

                    case HardwareType.GpuNvidia:
                    case HardwareType.GpuAmd:
                    case HardwareType.GpuIntel:
                        _gpuReaders.Add(new GpuReader(hw));
                        break;

                    case HardwareType.Storage:
                        _storageReaders.Add(new StorageReader(hw, diskOrder++));
                        break;

                    case HardwareType.Network:
                        // 预先过滤虚拟与常见回环网卡
                        if (!hw.Name.Contains("本地连接*") &&
                            !hw.Name.Contains("Local") &&
                            !hw.Name.Contains("Loopback", StringComparison.OrdinalIgnoreCase))
                        {
                            _networkReaders.Add(new NetworkReader(hw));
                        }
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

            // 4. Motherboard (主板、风扇、电压、VRM与主板温度)
            foreach (var mb in _motherboardReaders)
            {
                mb.Update();
                var mbDto = mb.Read();
                if (mbDto != null)
                {
                    envelope.Body.Add(new Dictionary<string, object>(1)
                    {
                        ["Motherboard"] = mbDto
                    });
                }
            }

            // 5. Storage (分频更新，显著降低高延迟IOCTL与内核切换)
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

            // 6. Network (过滤无流量网卡后按 Network0, Network1... 编号)
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
        private static double Round1(double v) => Math.Round(v, 1, MidpointRounding.AwayFromZero);

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

        private sealed class CoreLoadBinding
        {
            public string Key { get; }
            public ISensor[] Sensors { get; }

            public CoreLoadBinding(string key, ISensor[] sensors)
            {
                Key = key;
                Sensors = sensors;
            }

            public double? Read()
            {
                if (Sensors.Length == 1)
                {
                    var val = Sensors[0].Value;
                    return val.HasValue ? Round2(val.Value) : null;
                }

                double sum = 0;
                int count = 0;
                for (int i = 0; i < Sensors.Length; i++)
                {
                    var val = Sensors[i].Value;
                    if (val.HasValue)
                    {
                        sum += val.Value;
                        count++;
                    }
                }

                return count > 0 ? Round2(sum / count) : null;
            }
        }

        private sealed class CpuReader
        {
            private readonly IHardware _hw;
            private readonly string _name;
            private readonly ISensor? _totalLoad;
            private readonly ISensor? _coreMaxLoad;
            private readonly ISensor? _packageTemp;
            private readonly ISensor? _coreAvgTemp;
            private readonly ISensor? _coreMaxTemp;
            private readonly ISensor? _coreVoltage;
            private readonly ISensor? _power;
            private readonly ISensor? _corePower;
            private readonly (string Key, ISensor Sensor)[] _clocks;
            private readonly (string Key, ISensor Sensor)[] _coreTemps;
            private readonly (string Key, ISensor Sensor)[] _threadLoads;
            private readonly CoreLoadBinding[] _coreLoadBindings;
            private readonly int _cores;
            private readonly int _threads;

            public CpuReader(IHardware hw)
            {
                _hw = hw;
                _name = hw.Name;
                (_cores, _threads) = WmiHelper.GetCpuCoreInfo();

                var clocks = new List<(string Key, ISensor Sensor)>();
                var coreTemps = new List<(string Key, ISensor Sensor)>();
                var threadLoads = new List<(string Key, ISensor Sensor)>();
                var coreGroups = new Dictionary<int, List<ISensor>>();

                // 统计是否存在 P-Core 架构以自适应物理核命名 (PCore vs CPUCore)
                int pCoreCount = 0;
                foreach (var s in hw.Sensors)
                {
                    if (s.SensorType == SensorType.Clock && s.Name.Contains("P-Core", StringComparison.OrdinalIgnoreCase))
                    {
                        pCoreCount++;
                    }
                }

                foreach (var s in hw.Sensors)
                {
                    if (s.SensorType == SensorType.Load)
                    {
                        if (s.Name.Contains("Total", StringComparison.OrdinalIgnoreCase))
                        {
                            _totalLoad ??= s;
                        }
                        else if (s.Name.Contains("Core Max", StringComparison.OrdinalIgnoreCase))
                        {
                            _coreMaxLoad ??= s;
                        }
                        else if (s.Name.StartsWith("CPU Core #", StringComparison.OrdinalIgnoreCase))
                        {
                            // 解析核心编号：例如 "CPU Core #1 Thread #1" 或 "CPU Core #7"
                            int start = "CPU Core #".Length;
                            int end = start;
                            while (end < s.Name.Length && char.IsDigit(s.Name[end]))
                                end++;

                            if (end > start && int.TryParse(s.Name.AsSpan(start, end - start), out int coreIdx))
                            {
                                if (!coreGroups.TryGetValue(coreIdx, out var sensorList))
                                {
                                    sensorList = new List<ISensor>(2);
                                    coreGroups[coreIdx] = sensorList;
                                }
                                sensorList.Add(s);
                            }

                            var cleanThreadKey = s.Name
                                .Replace(" ", "")
                                .Replace("#", "")
                                .Replace("P-Core", "PCore")
                                .Replace("E-Core", "ECore");
                            threadLoads.Add((cleanThreadKey, s));
                        }
                    }
                    else if (s.SensorType == SensorType.Temperature)
                    {
                        if (s.Name.Contains("Package"))
                            _packageTemp ??= s;
                        else if (s.Name.Contains("Core Average"))
                            _coreAvgTemp ??= s;
                        else if (s.Name.Contains("Core Max"))
                            _coreMaxTemp ??= s;
                        else if (s.Name.Contains("Core #") && !s.Name.Contains("Distance", StringComparison.OrdinalIgnoreCase))
                        {
                            var cleanKey = s.Name
                                .Replace(" ", "")
                                .Replace("#", "")
                                .Replace("P-Core", "PCore")
                                .Replace("E-Core", "ECore");
                            coreTemps.Add((cleanKey, s));
                        }
                    }
                    else if (s.SensorType == SensorType.Voltage && s.Name.Contains("Core"))
                        _coreVoltage ??= s;
                    else if (s.SensorType == SensorType.Power)
                    {
                        if (s.Name.Contains("Package") || s.Name.Contains("CPU"))
                            _power ??= s;
                        else if (s.Name.Contains("Cores"))
                            _corePower ??= s;
                    }
                    else if (s.SensorType == SensorType.Clock)
                    {
                        var n = s.Name;
                        if (n.Contains("Core") || n.Contains("CPU") || n.Contains("Bus"))
                        {
                            var cleanKey = n
                                .Replace(" ", "")
                                .Replace("#", "")
                                .Replace("P-Core", "PCore")
                                .Replace("E-Core", "ECore");
                            clocks.Add((cleanKey, s));
                        }
                    }
                }

                _clocks = clocks.ToArray();
                _coreTemps = coreTemps.ToArray();
                _threadLoads = threadLoads.ToArray();

                // 构建物理核心负载绑定映射
                var bindings = new List<CoreLoadBinding>(coreGroups.Count);
                foreach (var kvp in coreGroups.OrderBy(x => x.Key))
                {
                    int coreIdx = kvp.Key;
                    string coreKey;
                    if (pCoreCount > 0)
                    {
                        coreKey = coreIdx <= pCoreCount ? $"PCore{coreIdx}" : $"ECore{coreIdx - pCoreCount}";
                    }
                    else
                    {
                        coreKey = $"CPUCore{coreIdx}";
                    }

                    bindings.Add(new CoreLoadBinding(coreKey, kvp.Value.ToArray()));
                }
                _coreLoadBindings = bindings.ToArray();
            }

            public void Update() => UpdateHardware(_hw);

            public CpuDto Read()
            {
                var dto = new CpuDto
                {
                    CPUInfo = _name,
                    TotalLoad = _totalLoad?.Value != null ? Round0(_totalLoad.Value.Value) : null,
                    CoreMaxLoad = _coreMaxLoad?.Value != null ? Round2(_coreMaxLoad.Value.Value) : null,
                    CPUPackageTemp = _packageTemp?.Value != null ? Round0(_packageTemp.Value.Value) : null,
                    CoreAverageTemp = _coreAvgTemp?.Value != null ? Round0(_coreAvgTemp.Value.Value) : null,
                    CoreMaxTemp = _coreMaxTemp?.Value != null ? Round0(_coreMaxTemp.Value.Value) : null,
                    CPUVoltage = _coreVoltage?.Value != null ? Round2(_coreVoltage.Value.Value) : null,
                    Power = _power?.Value != null ? Round0(_power.Value.Value) : null,
                    CorePower = _corePower?.Value != null ? Round0(_corePower.Value.Value) : null,
                    Cores = _cores > 0 ? _cores : null,
                    Threads = _threads > 0 ? _threads : null,
                };

                for (int i = 0; i < _clocks.Length; i++)
                {
                    var val = _clocks[i].Sensor.Value;
                    if (val.HasValue)
                    {
                        dto.Clock[_clocks[i].Key] = Round2(val.Value);
                    }
                }

                for (int i = 0; i < _coreTemps.Length; i++)
                {
                    var val = _coreTemps[i].Sensor.Value;
                    if (val.HasValue)
                    {
                        dto.CoreTemperatures[_coreTemps[i].Key] = Round0(val.Value);
                    }
                }

                for (int i = 0; i < _coreLoadBindings.Length; i++)
                {
                    var val = _coreLoadBindings[i].Read();
                    if (val.HasValue)
                    {
                        dto.CoreLoads[_coreLoadBindings[i].Key] = val.Value;
                    }
                }

                for (int i = 0; i < _threadLoads.Length; i++)
                {
                    var val = _threadLoads[i].Sensor.Value;
                    if (val.HasValue)
                    {
                        dto.ThreadLoads[_threadLoads[i].Key] = Round2(val.Value);
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
            private readonly WmiHelper.MemorySummary _memSummary;
            private readonly IHardware? _virtualMemHw;

            public MemoryReader(IHardware hw, IHardware? virtualMemHw = null)
            {
                _hw = hw;
                _virtualMemHw = virtualMemHw;
                _memSummary = WmiHelper.GetMemorySummary();

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

            public void Update()
            {
                UpdateHardware(_hw);
                if (_virtualMemHw != null)
                    UpdateHardware(_virtualMemHw);
            }

            public MemoryDto Read()
            {
                double? used = _used?.Value != null ? Round2(_used.Value.Value) : null;
                double? free = _free?.Value != null ? Round2(_free.Value.Value) : null;
                double? total = (used.HasValue && free.HasValue)
                    ? Round2(used.Value + free.Value)
                    : (_memSummary.TotalGB > 0 ? _memSummary.TotalGB : null);

                var dto = new MemoryDto
                {
                    MemoryInfo = !string.IsNullOrEmpty(_memSummary.FormattedString)
                        ? _memSummary.FormattedString
                        : "Generic Memory",
                    MemoryLoad = _load?.Value != null ? Round2(_load.Value.Value) : null,
                    Used = used,
                    Free = free,
                    Total = total,
                    Speed = _memSummary.Speed > 0 ? _memSummary.Speed : null,
                    Type = _memSummary.Type,
                    Slots = _memSummary.Slots
                };

                if (_virtualMemHw != null)
                {
                    foreach (var s in _virtualMemHw.Sensors)
                    {
                        if (s.SensorType == SensorType.Data && s.Name.Contains("Used") && s.Value.HasValue)
                            dto.VirtualUsed = Round2(s.Value.Value);
                        else if (s.SensorType == SensorType.Data && s.Name.Contains("Available") && s.Value.HasValue)
                            dto.VirtualFree = Round2(s.Value.Value);
                    }
                    if (dto.VirtualUsed.HasValue && dto.VirtualFree.HasValue)
                        dto.VirtualTotal = Round2(dto.VirtualUsed.Value + dto.VirtualFree.Value);
                }

                return dto;
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
            private readonly ISensor? _memTemp;
            private readonly ISensor? _fanSpeed;
            private readonly ISensor? _fanLoad;
            private readonly List<ISensor> _fans = new();
            private readonly ISensor? _memTotal;
            private readonly ISensor? _memFree;
            private readonly ISensor? _memUsed;
            private readonly ISensor? _power;
            private readonly ISensor? _voltage;
            private readonly ISensor? _videoEngineLoad;

            public GpuReader(IHardware hw)
            {
                _hw = hw;
                _name = hw.Name;

                foreach (var s in hw.Sensors)
                {
                    if (s.SensorType == SensorType.Load)
                    {
                        if (s.Name.Contains("Core"))
                            _coreLoad ??= s;
                        else if (s.Name.Contains("Memory") && !s.Name.Contains("Controller"))
                            _memLoad ??= s;
                        else if (s.Name.Contains("Video Engine") || s.Name.Contains("Video Encode"))
                            _videoEngineLoad ??= s;
                    }
                    else if (s.SensorType == SensorType.Clock)
                    {
                        if (s.Name.Contains("Core"))
                            _coreClock ??= s;
                        else if (s.Name.Contains("Memory"))
                            _memClock ??= s;
                    }
                    else if (s.SensorType == SensorType.Temperature)
                    {
                        if (s.Name.Contains("Hot Spot"))
                            _hotSpotTemp ??= s;
                        else if (s.Name.Contains("Memory") || s.Name.Contains("VRAM") || s.Name.Contains("Junction"))
                            _memTemp ??= s;
                        else if (s.Name.Contains("Core") || s.Name.Contains("GPU"))
                            _temp ??= s;
                    }
                    else if (s.SensorType == SensorType.Power)
                    {
                        if (s.Name.Contains("Package") || s.Name.Contains("GPU") || s.Name.Contains("Board"))
                            _power ??= s;
                    }
                    else if (s.SensorType == SensorType.Voltage)
                    {
                        if (s.Name.Contains("Core"))
                            _voltage ??= s;
                    }
                    else if (s.SensorType == SensorType.Fan)
                    {
                        _fanSpeed ??= s;
                        _fans.Add(s);
                    }
                    else if (s.SensorType == SensorType.Control)
                    {
                        _fanLoad ??= s;
                    }
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
                var dto = new GpuDto
                {
                    GPUInfo = _name,
                    GPULoad = _coreLoad?.Value != null ? Round0(_coreLoad.Value.Value) : null,
                    GPUMemoryLoad = _memLoad?.Value != null ? Round2(_memLoad.Value.Value) : null,
                    GPUCoreClock = _coreClock?.Value != null ? Round0(_coreClock.Value.Value) : null,
                    GPUMemoryClock = _memClock?.Value != null ? Round0(_memClock.Value.Value) : null,
                    Temp = _temp?.Value != null ? Round0(_temp.Value.Value) : null,
                    HotSpotTemp = _hotSpotTemp?.Value != null ? Round0(_hotSpotTemp.Value.Value) : null,
                    MemoryTemp = _memTemp?.Value != null ? Round0(_memTemp.Value.Value) : null,
                    Power = _power?.Value != null ? Round0(_power.Value.Value) : null,
                    GPUVoltage = _voltage?.Value != null ? Round2(_voltage.Value.Value) : null,
                    FanSpeed = _fanSpeed?.Value != null ? Round0(_fanSpeed.Value.Value) : null,
                    FanLoad = _fanLoad?.Value != null ? Round0(_fanLoad.Value.Value) : null,
                    GPUMemoryTotal = _memTotal?.Value != null ? Round0(_memTotal.Value.Value) : null,
                    GPUMemoryFree = _memFree?.Value != null ? Round0(_memFree.Value.Value) : null,
                    GPUMemoryUsed = _memUsed?.Value != null ? Round0(_memUsed.Value.Value) : null,
                    VideoEngineLoad = _videoEngineLoad?.Value != null ? Round0(_videoEngineLoad.Value.Value) : null,
                };

                foreach (var f in _fans)
                {
                    if (f.Value.HasValue && f.Value.Value > 0)
                    {
                        dto.Fans[f.Name] = Round0(f.Value.Value);
                    }
                }

                return dto;
            }
        }

        private sealed class MotherboardReader
        {
            private readonly IHardware _hw;
            private readonly string _name;
            private readonly string _manufacturer;
            private readonly string _biosVersion;
            private readonly List<ISensor> _fans = new();
            private readonly List<ISensor> _fanLoads = new();
            private readonly List<ISensor> _temperatures = new();
            private readonly List<ISensor> _voltages = new();

            public MotherboardReader(IHardware hw)
            {
                _hw = hw;
                _name = hw.Name;
                var (mfg, _, bios) = WmiHelper.GetMotherboardDetails();
                _manufacturer = mfg;
                _biosVersion = bios;

                CollectSensors(hw);
            }

            private void CollectSensors(IHardware hw)
            {
                foreach (var s in hw.Sensors)
                {
                    switch (s.SensorType)
                    {
                        case SensorType.Fan:
                            _fans.Add(s);
                            break;
                        case SensorType.Control:
                            _fanLoads.Add(s);
                            break;
                        case SensorType.Temperature:
                            _temperatures.Add(s);
                            break;
                        case SensorType.Voltage:
                            _voltages.Add(s);
                            break;
                    }
                }

                foreach (var sub in hw.SubHardware)
                {
                    CollectSensors(sub);
                }
            }

            public void Update() => UpdateHardware(_hw);

            public MotherboardDto Read()
            {
                var dto = new MotherboardDto
                {
                    MotherboardInfo = _name,
                    Manufacturer = _manufacturer,
                    BiosVersion = _biosVersion
                };

                foreach (var f in _fans)
                {
                    if (f.Value.HasValue && f.Value.Value > 0)
                    {
                        dto.Fans[f.Name] = Round0(f.Value.Value);
                    }
                }

                foreach (var fl in _fanLoads)
                {
                    if (fl.Value.HasValue && fl.Value.Value > 0)
                    {
                        dto.FansLoads[fl.Name] = Round0(fl.Value.Value);
                    }
                }

                foreach (var t in _temperatures)
                {
                    if (t.Value.HasValue && t.Value.Value > 0 && t.Value.Value < 120)
                    {
                        dto.Temperatures[t.Name] = Round0(t.Value.Value);
                    }
                }

                foreach (var v in _voltages)
                {
                    if (v.Value.HasValue && (v.Name.Contains("Vcore", StringComparison.OrdinalIgnoreCase) ||
                                             v.Name.Contains("12V") ||
                                             v.Name.Contains("5V") ||
                                             v.Name.Contains("3.3V") ||
                                             v.Name.Contains("+3V")))
                    {
                        dto.Voltages[v.Name] = Round2(v.Value.Value);
                    }
                }

                return dto;
            }
        }

        private sealed class StorageReader
        {
            private readonly IHardware _hw;
            private readonly string _name;
            private readonly string? _partitions;
            private readonly ISensor? _temp;
            private readonly ISensor? _usedPercent;
            private readonly ISensor? _totalSpace;
            private readonly ISensor? _freeSpace;
            private readonly ISensor? _health;
            private readonly ISensor? _read;
            private readonly ISensor? _written;
            private readonly ISensor? _readSpeed;
            private readonly ISensor? _writeSpeed;
            private StorageDto? _cachedDto;

            public StorageReader(IHardware hw, int diskOrder)
            {
                _hw = hw;

                // 1. 彻底清洗磁盘名称，去除 \0、控制字符与空白（防止 NVMe 报文出现空字节字符）
                string cleanName = hw.Name?.Trim('\0', ' ', '\t', '\r', '\n') ?? string.Empty;

                // 2. 尝试从 Identifier（如 /nvme/0, /storage/1）解析硬盘下标
                int diskIndex = diskOrder;
                var idStr = hw.Identifier.ToString();
                int lastSlash = idStr.LastIndexOf('/');
                if (lastSlash >= 0 && int.TryParse(idStr.Substring(lastSlash + 1), out int parsedIdx))
                {
                    diskIndex = parsedIdx;
                }

                // 3. 关联 WMI 硬盘元数据（型号兜底 + 挂载盘符）
                var diskInfo = WmiHelper.GetDiskDriveInfo(diskIndex);
                if (string.IsNullOrWhiteSpace(cleanName))
                {
                    cleanName = !string.IsNullOrWhiteSpace(diskInfo.Model) ? diskInfo.Model : $"Disk #{diskIndex}";
                }
                _name = cleanName;
                _partitions = diskInfo.Partitions;

                // 4. 绑定各传感器（优先抓取有效主温度，排除 Warning/Critical 阈值传感器）
                foreach (var s in hw.Sensors)
                {
                    if (s.SensorType == SensorType.Temperature)
                    {
                        if (s.Name.Contains("Composite") || (s.Name.Contains("Temperature") && !s.Name.Contains("Warning") && !s.Name.Contains("Critical")))
                        {
                            _temp ??= s;
                        }
                    }
                    else if (s.SensorType == SensorType.Load && (s.Name.Contains("Used Space") || s.Name.Contains("Used")))
                        _usedPercent ??= s;
                    else if (s.SensorType == SensorType.Data)
                    {
                        if (s.Name.Contains("Total Space"))
                            _totalSpace ??= s;
                        else if (s.Name.Contains("Free Space"))
                            _freeSpace ??= s;
                        else if (s.Name.Contains("Read") && !s.Name.Contains("Rate"))
                            _read ??= s;
                        else if (s.Name.Contains("Written") && !s.Name.Contains("Rate"))
                            _written ??= s;
                    }
                    else if (s.SensorType == SensorType.Level && s.Name.Contains("Life"))
                    {
                        _health ??= s;
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
                var dto = new StorageDto
                {
                    DiskInfo = _name,
                    Partitions = !string.IsNullOrEmpty(_partitions) ? _partitions : null
                };

                // 排除 0°C 或异常数值
                if (_temp?.Value != null && _temp.Value.Value > 0 && _temp.Value.Value < 120)
                {
                    dto.Temp = Round0(_temp.Value.Value);
                    has = true;
                }

                if (_usedPercent?.Value != null)
                {
                    dto.UsedPercent = Round2(_usedPercent.Value.Value);
                    has = true;
                }

                if (_totalSpace?.Value != null)
                {
                    dto.TotalSpace = Round1(_totalSpace.Value.Value);
                    has = true;
                }

                if (_freeSpace?.Value != null)
                {
                    dto.FreeSpace = Round1(_freeSpace.Value.Value);
                    has = true;
                }

                if (dto.TotalSpace.HasValue && dto.FreeSpace.HasValue)
                {
                    dto.UsedSpace = Round1(dto.TotalSpace.Value - dto.FreeSpace.Value);
                }

                if (_health?.Value != null)
                {
                    dto.Health = Math.Min(100, Round0(_health.Value.Value));
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

        private sealed class NetworkReader
        {
            private readonly IHardware _hw;
            private readonly string _name;
            private readonly string? _adapterName;
            private readonly string? _ipv4;
            private readonly string? _linkSpeed;
            private readonly string? _macAddress;
            private readonly ISensor? _downSpeed;
            private readonly ISensor? _upSpeed;
            private readonly ISensor? _downloaded;
            private readonly ISensor? _uploaded;

            public NetworkReader(IHardware hw)
            {
                _hw = hw;
                _name = hw.Name;

                var nic = WmiHelper.GetNetworkAdapterDetails(hw.Name);
                _adapterName = !string.IsNullOrEmpty(nic.Description) ? nic.Description : null;
                _ipv4 = !string.IsNullOrEmpty(nic.IPv4) ? nic.IPv4 : null;
                _linkSpeed = !string.IsNullOrEmpty(nic.LinkSpeed) ? nic.LinkSpeed : null;
                _macAddress = !string.IsNullOrEmpty(nic.MacAddress) ? nic.MacAddress : null;

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
                    AdapterName = _adapterName,
                    IPv4 = _ipv4,
                    LinkSpeed = _linkSpeed,
                    MacAddress = _macAddress,
                    DownloadSpeed = downSpeed,
                    UploadSpeed = upSpeed,
                    DataDownloaded = downloaded,
                    DataUploaded = uploaded
                };
            }
        }
    }
}
