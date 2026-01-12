// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CmdPal.Core.Common;

// using Serilog;
namespace CoreWidgetProvider.Helpers;

internal sealed partial class CPUStats : IDisposable
{
    // CPU counters
    private readonly PerformanceCounter _procPerf = new("Processor Information", "% Processor Utility", "_Total");
    private readonly PerformanceCounter _procPerformance = new("Processor Information", "% Processor Performance", "_Total");
    private readonly PerformanceCounter _procFrequency = new("Processor Information", "Processor Frequency", "_Total");
    private readonly Dictionary<Process, PerformanceCounter> _cpuCounters = new();

    // private readonly ILogger _log = Log.ForContext("SourceContext", nameof(CPUStats));
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

        InitCPUPerfCounters();
    }

    private void InitCPUPerfCounters()
    {
        var allProcesses = Process.GetProcesses().Where(p => (long)p.MainWindowHandle != 0);

        foreach (var process in allProcesses)
        {
            _cpuCounters.Add(process, new PerformanceCounter("Process", "% Processor Time", process.ProcessName, true));
        }
    }

    public void GetData(bool includeTopProcesses)
    {
        var timer = Stopwatch.StartNew();
        CpuUsage = _procPerf.NextValue() / 100;
        var usageMs = timer.ElapsedMilliseconds;
        CpuSpeed = _procFrequency.NextValue() * (_procPerformance.NextValue() / 100);
        var speedMs = timer.ElapsedMilliseconds - usageMs;
        lock (CpuChartValues)
        {
            ChartHelper.AddNextChartValue(CpuUsage * 100, CpuChartValues);
        }

        var chartMs = timer.ElapsedMilliseconds - speedMs;

        var processCPUUsages = new Dictionary<Process, float>();

        if (includeTopProcesses)
        {
            foreach (var processCounter in _cpuCounters)
            {
                try
                {
                    // process might be terminated
                    processCPUUsages.Add(processCounter.Key, processCounter.Value.NextValue() / Environment.ProcessorCount);
                }
                catch (InvalidOperationException)
                {
                    // _log.Information($"ProcessCounter Key {processCounter.Key} no longer exists, removing from _cpuCounters.");
                    _cpuCounters.Remove(processCounter.Key);
                }
                catch (Exception)
                {
                    // _log.Error(ex, "Error going through process counters.");
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
        CoreLogger.LogDebug($"[{usageMs}]+[{speedMs}]+[{chartMs}]+[{processesMs}]=[{total}]");
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
        _procPerf.Dispose();
        _procPerformance.Dispose();
        _procFrequency.Dispose();

        foreach (var counter in _cpuCounters.Values)
        {
            counter.Dispose();
        }
    }
}
