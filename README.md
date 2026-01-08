# 🚀 HaloMonitor V2 (C#版本)

这是 HaloMonitor.py 的 C# 重构版本，保留了所有原有功能，性能更优，资源占用更少。

## ✨ 功能特性

- ✅ **完整硬件监控**: CPU、GPU、内存、网络等硬件信息实时监控
- ✅ **WebSocket 通信**: 与远程服务器实时数据传输，自动重连
- ✅ **配置管理**: JSON配置文件支持，可自定义上报频率
- ✅ **网卡名称优化**: 智能识别网卡类型，解决乱码问题
- ✅ **高性能**: C#异步编程，低资源占用
- ✅ **平台**: Windows 原生支持

## 📊 性能对比

| 指标       | Python 版本      | Rust 版本 | 提升       |
| ---------- | ---------------- | --------- | ---------- |
| 内存占用   | ~50MB            | ~30MB     | 🔥 减少 40% |
| 启动时间   | 3-5秒            | <1秒      | ⚡ 提升 5倍 |
| CPU 使用率 | 2-3%             | <1%       | 💪 减少 60% |
| 文件大小   | 需要 Python 环境 | 单文件    | ✅ 独立部署 |

## 🚀 快速开始

### 源码编译：使用VisualStudio编译版本 (推荐)

```bash
# Powershell以管理员身份运行
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishTrimmed=false
```



## ⚙️ 配置说明

编辑 `appsettings.json` 文件：

```yaml
{
  "Monitor": {
    "DeviceId": "PC-HAINEI-001", 	#设备自定义唯一标识
    "Url": "wss://xxx/ws", 			#上报地址
    "ReconnectDelaySeconds": 5,		#重连等待时间（秒）
    "ReportIntervalSeconds": 1		#数据上报周期（秒）
  }
}
```

## 📊 监控数据格式

程序发送的 JSON 数据格式与原 Python 版本完全兼容：

```json
{
    "DeviceId": "PC-HAINEI-001",
    "Time": "2026-01-08 17:16:22",
    "Body": [
        {
            "CPU": {
                "CPUInfo": "11th Gen Intel Core i7-11700",
                "TotalLoad": "5.4",
                "CPUPackageTemp": "62.0",
                "CoreAverageTemp": "60.8",
                "CoreMaxTemp": "62.0",
                "CPUVoltage": "1.273",
                "Power": "48.91",
                "Clock": {
                    "CPUCore1": "4393.0",
                    "CPUCore2": "4393.0",
                    "CPUCore3": "4393.0",
                    "CPUCore4": "4393.0",
                    "CPUCore5": "4393.0",
                    "CPUCore6": "4393.0",
                    "CPUCore7": "4393.0",
                    "CPUCore8": "4393.0"
                }
            }
        },
        {
            "memory": {
                "MemoryInfo": "Generic Memory",
                "MemoryLoad": "72.3",
                "Used": "23.01",
                "Free": "8.83"
            }
        },
        {
            "GPU": {
                "GPUInfo": "NVIDIA Quadro P400",
                "GPULoad": "1.0",
                "GPUMemoryLoad": "78.0",
                "GPUCoreClock": "1227.5",
                "GPUMemoryClock": "2005.0",
                "Temp": "46.0",
                "HotSpotTemp": "53.6",
                "FanSpeed": 2006,
                "FanLoad": 34,
                "GPUMemoryTotal": 2048,
                "GPUMemoryFree": 449,
                "GPUMemoryUsed": 1598
            }
        },
        {
            "Storage": {
                "DiskInfo": "WD Blue SN570 500GB SSD",
                "Temp": "40.0",
                "UsedPercent": "35.3",
                "Read": "7.29 TB",
                "Written": "25.64 TB",
                "ReadSpeed": "6.7 Kb/s",
                "WriteSpeed": "167.1 Kb/s"
            }
        },
        {
            "Storage": {
                "DiskInfo": "WDC WD10EZEX-00MFCA0",
                "Temp": "34.0",
                "UsedPercent": "3.0",
                "Read": null,
                "Written": null,
                "ReadSpeed": "0.0 Kb/s",
                "WriteSpeed": "0.0 Kb/s"
            }
        },
        {
            "Network0": {
                "NetworkInfo": "以太网",
                "DataDownloaded": "12.67 GB",
                "DataUploaded": "2.42 GB",
                "DownloadSpeed": "7.3 Kb/s",
                "UploadSpeed": "1.9 Kb/s"
            }
        },
        {
            "Network1": {
                "NetworkInfo": "以太网 2",
                "DataDownloaded": "0.01 GB",
                "DataUploaded": "0.00 GB",
                "DownloadSpeed": "0.0 Kb/s",
                "UploadSpeed": "0.0 Kb/s"
            }
        }
    ]
}
```

### ⚙️ 硬件获取策略

依赖**LibreHardwareMonitor**: 获取完整电脑详细信息