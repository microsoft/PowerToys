// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class CPUStats : PerformanceCounterSourceBase, IDisposable
{
    // CPU counters
    private readonly PerformanceCounter? _procPerf;
    private readonly PerformanceCounter? _procPerformance;
    private readonly PerformanceCounter? _procFrequency;
    private readonly Dictionary<Process, PerformanceCounter> _cpuCounters = new();
    private bool _processCountersInitialized;
    private bool _cpuCounterReadFailureLogged;
    private bool _processCounterEnumerationFailureLogged;
    private bool _processCounterReadFailureLogged;

    internal sealed class ProcessStats
    {
        public Process? Process { get; set; }

        public float CpuUsage { get; set; }
    }

    public float CpuUsage { get; set; }

    public float CpuSpeed { get; set; }

    public ProcessStats[] ProcessCPUStats { get; set; }

    public List<float> CpuChartValues { get; set; } = new();

    public CPUStats()
    {
        CpuUsage = 0;
        ProcessCPUStats =
        [
            new ProcessStats(),
            new ProcessStats(),
            new ProcessStats()
        ];

        _procPerf = CreatePerformanceCounter("Processor Information", "% Processor Utility", "_Total");
        _procPerformance = CreatePerformanceCounter("Processor Information", "% Processor Performance", "_Total");
        _procFrequency = CreatePerformanceCounter("Processor Information", "Processor Frequency", "_Total");
    }

    private void EnsureCPUProcessCountersInitialized()
    {
        if (_processCountersInitialized)
        {
            return;
        }

        _processCountersInitialized = true;

        try
        {
            var allProcesses = Process.GetProcesses().Where(p => (long)p.MainWindowHandle != 0);

            foreach (var process in allProcesses)
            {
                try
                {
                    var counter = CreatePerformanceCounter("Process", "% Processor Time", process.ProcessName, logFailure: false);
                    if (counter is not null)
                    {
                        _cpuCounters.Add(process, counter);
                    }
                }
                catch (Exception)
                {
                    // Skip processes whose counters cannot be created.
                }
            }
        }
        catch (Exception ex)
        {
            LogFailureOnce(ref _processCounterEnumerationFailureLogged, "Failed to initialize CPU process performance counters.", ex);
        }
    }

    public void GetData(bool includeTopProcesses)
    {
        try
        {
            var timer = Stopwatch.StartNew();
            if (_procPerf is not null)
            {
                CpuUsage = _procPerf.NextValue() / 100;
            }

            var usageMs = timer.ElapsedMilliseconds;
            if (_procFrequency is not null && _procPerformance is not null)
            {
                CpuSpeed = _procFrequency.NextValue() * (_procPerformance.NextValue() / 100);
            }

            var speedMs = timer.ElapsedMilliseconds - usageMs;
            lock (CpuChartValues)
            {
                ChartHelper.AddNextChartValue(CpuUsage * 100, CpuChartValues);
            }

            var chartMs = timer.ElapsedMilliseconds - speedMs;

            var processCPUUsages = new Dictionary<Process, float>();

            if (includeTopProcesses)
            {
                EnsureCPUProcessCountersInitialized();

                var countersToRemove = new List<Process>();
                foreach (var processCounter in _cpuCounters.ToArray())
                {
                    try
                    {
                        // process might be terminated
                        processCPUUsages.Add(processCounter.Key, processCounter.Value.NextValue() / Environment.ProcessorCount);
                    }
                    catch (InvalidOperationException)
                    {
                        countersToRemove.Add(processCounter.Key);
                    }
                    catch (Exception ex)
                    {
                        LogFailureOnce(ref _processCounterReadFailureLogged, "Failed while reading CPU process performance counters.", ex);
                    }
                }

                foreach (var process in countersToRemove)
                {
                    if (_cpuCounters.Remove(process, out var counter))
                    {
                        counter.Dispose();
                    }
                }

                var cpuIndex = 0;
                foreach (var processCPUValue in processCPUUsages.OrderByDescending(x => x.Value).Take(3))
                {
                    ProcessCPUStats[cpuIndex].Process = processCPUValue.Key;
                    ProcessCPUStats[cpuIndex].CpuUsage = processCPUValue.Value;
                    cpuIndex++;
                }
            }

            timer.Stop();
            var total = timer.ElapsedMilliseconds;
            var processesMs = total - chartMs;

            // CoreLogger.LogDebug($"[{usageMs}]+[{speedMs}]+[{chartMs}]+[{processesMs}]=[{total}]");
        }
        catch (Exception ex)
        {
            LogFailureOnce(ref _cpuCounterReadFailureLogged, "Failed while reading CPU performance counters.", ex);
        }
    }

    internal string CreateCPUImageUrl()
    {
        return ChartHelper.CreateImageUrl(CpuChartValues, ChartHelper.ChartType.CPU);
    }

    internal string GetCpuProcessText(int cpuProcessIndex)
    {
        if (cpuProcessIndex >= ProcessCPUStats.Length)
        {
            return "no data";
        }

        return $"{ProcessCPUStats[cpuProcessIndex].Process?.ProcessName} ({ProcessCPUStats[cpuProcessIndex].CpuUsage / 100:p})";
    }

    internal void KillTopProcess(int cpuProcessIndex)
    {
        if (cpuProcessIndex >= ProcessCPUStats.Length)
        {
            return;
        }

        ProcessCPUStats[cpuProcessIndex].Process?.Kill();
    }

    public void Dispose()
    {
        _procPerf?.Dispose();
        _procPerformance?.Dispose();
        _procFrequency?.Dispose();

        foreach (var counter in _cpuCounters.Values)
        {
            counter.Dispose();
        }
    }
}
