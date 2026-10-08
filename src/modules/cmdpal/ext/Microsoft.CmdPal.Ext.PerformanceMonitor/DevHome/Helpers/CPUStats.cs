// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CmdPal.Ext.PerformanceMonitor;
using Microsoft.Win32;
using Windows.Win32;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class CPUStats : PerformanceCounterSourceBase, IDisposable
{
    private static readonly Lazy<string> CachedProcessorName = new(ReadProcessorName);

    // CPU counters
    private readonly PerformanceCounter? _procPerf;
    private readonly PerformanceCounter? _procPerformance;
    private readonly PerformanceCounter? _procFrequency;
    private readonly Dictionary<Process, PerformanceCounter> _cpuCounters = new();
    private bool _processCountersInitialized;
    private bool _cpuCounterReadFailureLogged;
    private bool _processCounterEnumerationFailureLogged;
    private bool _processCounterReadFailureLogged;

    // Per-processor utilization, read in one batch, only while a CPU card shows it.
    private PerformanceCounterCategory? _processorCategory;
    private bool _processorCategoryCreated;
    private Dictionary<string, CounterSample> _previousCoreSamples = [];
    private int _coreRequests;
    private bool _coreReadFailureLogged;

    internal sealed class ProcessStats
    {
        public Process? Process { get; set; }

        public float CpuUsage { get; set; }
    }

    public float CpuUsage { get; set; }

    public float CpuSpeed { get; set; }

    /// <summary>Gets or sets the nominal processor frequency in MHz.</summary>
    public float CpuBaseSpeed { get; set; }

    public uint ProcessCount { get; private set; }

    public uint ThreadCount { get; private set; }

    public uint HandleCount { get; private set; }

    public static string ProcessorName => CachedProcessorName.Value;

    public static int LogicalProcessorCount => Environment.ProcessorCount;

    public static TimeSpan Uptime => TimeSpan.FromMilliseconds(Environment.TickCount64);

    public ProcessStats[] ProcessCPUStats { get; set; }

    public List<float> CpuChartValues { get; set; } = new();

    /// <summary>
    /// Gets the utilization of each logical processor in percent, in processor order. It's empty
    /// unless a card has requested it with <see cref="RequestCoreUsage"/>.
    /// </summary>
    public float[] CoreUsage { get; private set; } = [];

    public CPUStats()
    {
        CpuUsage = 0;
        ProcessCPUStats =
        [
            new ProcessStats(),
            new ProcessStats(),
            new ProcessStats()
        ];

        // Use "% Processor Time" instead of "% Processor Utility": the latter is unbounded above 100%
        // when cores boost above their nominal base frequency (it is scaled by % Processor Performance),
        // which produced values like 144% in the dock under heavy load. % Processor Time is the same
        // counter Task Manager renders and is naturally bounded to 0-100%. See issue #46381.
        _procPerf = CreatePerformanceCounter("Processor Information", "% Processor Time", "_Total");
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
                var frequency = _procFrequency.NextValue();
                CpuBaseSpeed = frequency;
                CpuSpeed = frequency * (_procPerformance.NextValue() / 100);
            }

            ReadSystemCounts();

            if (Volatile.Read(ref _coreRequests) > 0)
            {
                ReadCoreUsage();
            }
            else if (CoreUsage.Length > 0)
            {
                CoreUsage = [];
                _previousCoreSamples = [];
            }

            var speedMs = timer.ElapsedMilliseconds - usageMs;
            lock (CpuChartValues)
            {
                ChartHelper.AddNextChartValue(CpuUsage * 100, CpuChartValues, PerformanceChartData.HistoryLength);
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

    /// <summary>Reads the system-wide process, thread, and handle counts in one call.</summary>
    private void ReadSystemCounts()
    {
        var info = new Windows.Win32.System.ProcessStatus.PERFORMANCE_INFORMATION
        {
            cb = (uint)Marshal.SizeOf<Windows.Win32.System.ProcessStatus.PERFORMANCE_INFORMATION>(),
        };
        if (PInvoke.GetPerformanceInfo(ref info, info.cb))
        {
            ProcessCount = info.ProcessCount;
            ThreadCount = info.ThreadCount;
            HandleCount = info.HandleCount;
        }
    }

    /// <summary>
    /// Starts or stops reading per-processor utilization. Only the CPU card shows it, so the dock
    /// and the overview don't pay for it.
    /// </summary>
    internal void RequestCoreUsage(bool request)
    {
        if (request)
        {
            Interlocked.Increment(ref _coreRequests);
        }
        else if (Interlocked.Decrement(ref _coreRequests) < 0)
        {
            Interlocked.Exchange(ref _coreRequests, 0);
        }
    }

    private void ReadCoreUsage()
    {
        if (!_processorCategoryCreated)
        {
            _processorCategoryCreated = true;
            _processorCategory = CreatePerformanceCounterCategory("Processor Information", logFailure: false);
        }

        if (_processorCategory is null)
        {
            return;
        }

        try
        {
            // One batch read for every processor, like the GPU engines.
            var categoryData = _processorCategory.ReadCategory();
            if (!categoryData.Contains("% Processor Time"))
            {
                return;
            }

            var samples = new Dictionary<string, CounterSample>();
            var cores = new List<(int Group, int Index, float Usage)>();
            foreach (InstanceData instance in categoryData["% Processor Time"].Values)
            {
                if (!TryParseProcessor(instance.InstanceName, out var group, out var index))
                {
                    continue;
                }

                samples[instance.InstanceName] = instance.Sample;
                var usage = 0f;
                if (_previousCoreSamples.TryGetValue(instance.InstanceName, out var previous))
                {
                    var cooked = CounterSampleCalculator.ComputeCounterValue(previous, instance.Sample);
                    usage = float.IsFinite(cooked) ? Math.Clamp(cooked, 0f, 100f) : 0f;
                }

                cores.Add((group, index, usage));
            }

            _previousCoreSamples = samples;
            cores.Sort(static (left, right) => left.Group != right.Group ? left.Group.CompareTo(right.Group) : left.Index.CompareTo(right.Index));
            CoreUsage = cores.Select(static core => core.Usage).ToArray();
        }
        catch (Exception ex)
        {
            LogFailureOnce(ref _coreReadFailureLogged, "Failed while reading per-processor performance counters.", ex);
        }
    }

    /// <summary>
    /// Parses a Processor Information instance name, such as <c>0,3</c> for processor 3 in group 0.
    /// Totals such as <c>_Total</c> and <c>0,_Total</c> return false.
    /// </summary>
    internal static bool TryParseProcessor(string instanceName, out int group, out int index)
    {
        group = 0;
        index = 0;
        var comma = instanceName.IndexOf(',');
        return comma > 0
            && int.TryParse(instanceName.AsSpan(0, comma), NumberStyles.None, CultureInfo.InvariantCulture, out group)
            && int.TryParse(instanceName.AsSpan(comma + 1), NumberStyles.None, CultureInfo.InvariantCulture, out index);
    }

    private static string ReadProcessorName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            var name = key?.GetValue("ProcessorNameString") as string;
            return string.IsNullOrWhiteSpace(name)
                ? string.Empty
                : string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }
        catch (Exception)
        {
            return string.Empty;
        }
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
