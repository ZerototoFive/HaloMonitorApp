# 🚀 HaloMonitor V2 (C#版本)

这是 HaloMonitor.py 的 C# 重构版本，保留了所有原有功能，性能更优，资源占用更少。

## ✨ 功能特性

- ✅ **完整硬件监控**: CPU、GPU、主板（多路风扇/VRM温度/核心与主板电压）、内存、硬盘（健康度/容量/盘符）、网络等硬件信息实时全面监控
- ✅ **智能硬件识别**: 告别通用占位名，自动识别物理内存规格与工作频率（如 `32 GB DDR4 @ 4000 MHz (2 x 16 GB)`）、主板型号、网卡硬件芯片（如 Intel I225-V）、磁盘挂载盘符与真实型号
- ✅ **修复NVMe固件Bug**: 彻底解决部分 NVMe 硬盘名称上报全 `\0` 空字节乱码（`\u0000\u0000...`）问题，智能通过 WMI 磁盘元数据兜底恢复准确型号
- ✅ **原生UTF-8 JSON输出**: 彻底消除非 ASCII 字符（如“以太网”）转义为 `\uXXXX` 的体验问题
- ✅ **WebSocket 通信**: 与远程服务器实时数据传输，自动重连
- ✅ **配置管理**: JSON配置文件支持，可自定义上报频率
- ✅ **高性能**: C#异步编程，传感器预计算绑定与磁盘分频轮询，极低 CPU 与内存占用
- ✅ **平台**: Windows 原生支持

## 📊 性能对比

| 指标       | Python 版本      | C# 版本 | 提升       |
| ---------- | ---------------- | ------- | ---------- |
| 内存占用   | ~50MB            | ~30MB   | 🔥 减少 40% |
| 启动时间   | 3-5秒            | <1秒    | ⚡ 提升 5倍 |
| CPU 使用率 | 2-3%             | <1%     | 💪 减少 60% |
| 文件大小   | 需要 Python 环境 | 单文件  | ✅ 独立部署 |

## 🚀 快速开始

### 源码编译：使用VisualStudio编译版本 (推荐)

```bash
# Powershell以管理员身份运行
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishTrimmed=false
```

## ⚙️ 配置说明

编辑 `appsettings.json` 文件：

```json
{
  "Monitor": {
    "DeviceId": "PC-HAINEI-001",
    "Url": "wss://xxx/ws",
    "ReconnectDelaySeconds": 5,
    "ReportIntervalSeconds": 1
  }
}
```

## 📊 监控数据格式

程序发送的 JSON 数据格式与原有格式向下兼容，并扩展了更全面的硬件参数：

```json
{
    "DeviceId": "PC-HAINEI-001",
    "Time": "2026-10-20 20:32:53",
    "Body": [
        {
            "CPU": {
                "CPUInfo": "12th Gen Intel Core i5-12600KF",
                "TotalLoad": 27,
                "CoreMaxLoad": 42.5,
                "CPUPackageTemp": 45,
                "CoreAverageTemp": 39,
                "CoreMaxTemp": 45,
                "CPUVoltage": 1.28,
                "Power": 72,
                "CorePower": 61,
                "Cores": 10,
                "Threads": 16,
                "Clock": {
                    "PCore1": 5081.26,
                    "PCore2": 5081.26,
                    "PCore3": 5081.26,
                    "PCore4": 5081.26,
                    "PCore5": 5081.26,
                    "PCore6": 5081.26,
                    "ECore1": 3586.77,
                    "ECore2": 3586.77,
                    "ECore3": 3586.77,
                    "ECore4": 3586.77,
                    "BusSpeed": 99.63
                },
                "CoreTemperatures": {
                    "PCore1": 42,
                    "PCore2": 38,
                    "PCore3": 45,
                    "PCore4": 39,
                    "PCore5": 40,
                    "PCore6": 41,
                    "ECore1": 36,
                    "ECore2": 37,
                    "ECore3": 37,
                    "ECore4": 37
                },
                "CoreLoads": {
                    "PCore1": 24.5,
                    "PCore2": 18.2,
                    "PCore3": 42.5,
                    "PCore4": 12.0,
                    "PCore5": 16.3,
                    "PCore6": 21.0,
                    "ECore1": 8.5,
                    "ECore2": 5.0,
                    "ECore3": 9.2,
                    "ECore4": 4.1
                },
                "ThreadLoads": {
                    "CPUCore1Thread1": 32.0,
                    "CPUCore1Thread2": 17.0,
                    "CPUCore2Thread1": 25.4,
                    "CPUCore2Thread2": 11.0,
                    "CPUCore3Thread1": 42.5,
                    "CPUCore3Thread2": 0.0,
                    "CPUCore4Thread1": 15.0,
                    "CPUCore4Thread2": 9.0,
                    "CPUCore5Thread1": 20.6,
                    "CPUCore5Thread2": 12.0,
                    "CPUCore6Thread1": 28.0,
                    "CPUCore6Thread2": 14.0,
                    "CPUCore7": 8.5,
                    "CPUCore8": 5.0,
                    "CPUCore9": 9.2,
                    "CPUCore10": 4.1
                }
            }
        },
        {
            "Memory": {
                "MemoryInfo": "32 GB DDR4 @ 4000 MHz (2 x 16 GB)",
                "MemoryLoad": 54.31,
                "Used": 17.28,
                "Free": 14.54,
                "Total": 31.82,
                "Speed": 4000,
                "Type": "DDR4",
                "Slots": "2 x 16 GB",
                "VirtualUsed": 18.2,
                "VirtualFree": 15.6,
                "VirtualTotal": 33.8
            }
        },
        {
            "GPU": {
                "GPUInfo": "NVIDIA GeForce RTX 4060 Ti",
                "GPULoad": 98,
                "GPUMemoryLoad": 45,
                "GPUCoreClock": 2820,
                "GPUMemoryClock": 9001,
                "Temp": 49,
                "HotSpotTemp": 58,
                "Power": 158,
                "GPUVoltage": 1.02,
                "FanSpeed": 1799,
                "FanLoad": 46,
                "Fans": {
                    "GPU Fan 1": 1799,
                    "GPU Fan 2": 1795
                },
                "GPUMemoryTotal": 8188,
                "GPUMemoryFree": 3318,
                "GPUMemoryUsed": 4640,
                "VideoEngineLoad": 0
            }
        },
        {
            "Motherboard": {
                "MotherboardInfo": "ASUS ROG STRIX Z690-A GAMING WIFI D4",
                "Manufacturer": "ASUSTeK COMPUTER INC.",
                "BiosVersion": "Rev 1.xx",
                "Fans": {
                    "Fan #1": 890,
                    "Fan #2": 1105,
                    "Fan #3": 558,
                    "Fan #4": 934
                },
                "FansLoads": {
                    "Fan #1": 32,
                    "Fan #2": 32,
                    "Fan #3": 32,
                    "Fan #4": 32
                },
                "Temperatures": {
                    "CPU Core": 38,
                    "Temperature #1": 34,
                    "VRM": 45
                },
                "Voltages": {
                    "Vcore": 1.22,
                    "+3.3V": 3.28
                }
            }
        },
        {
            "Storage": {
                "DiskInfo": "KIOXIA-EXCERIA G2 SSD",
                "Partitions": "E:, F:, G:",
                "Temp": 36,
                "UsedPercent": 43.25,
                "TotalSpace": 1000.2,
                "FreeSpace": 567.6,
                "UsedSpace": 432.6,
                "Health": 92,
                "Read": 28305,
                "Written": 19154,
                "ReadSpeed": 21,
                "WriteSpeed": 350
            }
        },
        {
            "Storage": {
                "DiskInfo": "NVMe INTEL SSDPEKKW256G8",
                "Partitions": "C:, D:",
                "Temp": 42,
                "UsedPercent": 73.48,
                "TotalSpace": 256.1,
                "FreeSpace": 67.9,
                "UsedSpace": 188.2,
                "Health": 100,
                "Read": 0,
                "Written": 0,
                "ReadSpeed": 549,
                "WriteSpeed": 975
            }
        },
        {
            "Network0": {
                "NetworkInfo": "以太网",
                "AdapterName": "Intel(R) Ethernet Controller (3) I225-V",
                "IPv4": "192.168.2.139",
                "LinkSpeed": "1000 Mbps",
                "DataDownloaded": 0.48,
                "DataUploaded": 0.51,
                "DownloadSpeed": 2.3,
                "UploadSpeed": 3.67
            }
        }
    ]
}
```

### ⚙️ 硬件获取策略

- **LibreHardwareMonitor**: 获取 CPU、GPU、主板各通道转速与温度、内存负载、硬盘实时 IO 与 SMART 动态指标
- **WMI / SMBIOS 底层缓存**:
  - 启动阶段一次性缓存物理内存硬件颗粒、频率、插槽、主板 BIOS、物理核心数等静态参数
  - 关联物理磁盘与逻辑盘符（`Win32_DiskDrive` + `Win32_LogicalDisk`），彻底修复 NVMe 名称空字节乱码问题
- **.NET 原生网络套件 (`NetworkInterface`)**:
  - 精确映射活跃网卡芯片型号、本机内网 IPv4、协商链路速率与 MAC 地址
