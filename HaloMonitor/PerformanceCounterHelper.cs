using System.Diagnostics;

namespace HaloMonitor
{
    public sealed class PerformanceCounterHelper : IDisposable
    {
        private readonly PerformanceCounter _cpuCounter;

        public PerformanceCounterHelper()
        {
            _cpuCounter = new PerformanceCounter(
                "Processor",
                "% Processor Time",
                "_Total"
            );

            // 预热（必须，否则第一次是0）
            _cpuCounter.NextValue();
        }

        public float GetCpuUsage()
        {
            return _cpuCounter.NextValue();
        }

        public void Dispose()
        {
            _cpuCounter?.Dispose();
        }
    }
}