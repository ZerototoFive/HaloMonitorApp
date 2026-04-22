using System.Management;

namespace HaloMonitor
{
    internal static class WmiHelper
    {
        // ================= CPU 温度（兼容性一般，但可兜底） =================
        public static float? GetCpuTemperature()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "root\\WMI",
                    "SELECT * FROM MSAcpi_ThermalZoneTemperature");

                foreach (ManagementObject obj in searcher.Get())
                {
                    var val = obj["CurrentTemperature"];
                    if (val == null) continue;

                    double temp = Convert.ToDouble(val);
                    // 转换：开尔文 *10 → 摄氏度
                    return (float)(temp / 10 - 273.15);
                }
            }
            catch { }

            return null;
        }

        // ================= CPU 电压（有些机器可用） =================
        public static float? GetCpuVoltage()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "root\\CIMV2",
                    "SELECT CurrentVoltage FROM Win32_Processor");

                foreach (ManagementObject obj in searcher.Get())
                {
                    var val = obj["CurrentVoltage"];
                    if (val == null) continue;

                    return Convert.ToSingle(val);
                }
            }
            catch { }

            return null;
        }

        // ================= CPU 功耗（几乎拿不到，仅占位） =================
        public static float? GetCpuPower()
        {
            return null; // WMI 基本拿不到真实功耗，保留接口
        }
    }
}