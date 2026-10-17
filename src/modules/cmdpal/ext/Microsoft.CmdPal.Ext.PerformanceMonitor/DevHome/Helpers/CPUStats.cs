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
using Windows.Win32.System.Power;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class CPUStats : PerformanceCounterSourceBase, IDisposable
{
    private static readonly Lazy<string> CachedProcessorName = new(ReadProcessorName);

    // A processor's rated speed doesn't change while it runs, so it's read once.
    private static readonly Lazy<uint> CachedBaseSpeed = new(ReadBaseSpeed);

    // A longer gap between per-processor samples means sampling stopped while no CPU card was
    // open, and utilization averaged over the gap wouldn't be current.
    private static readonly TimeSpan CoreSampleMaximumGap = TimeSpan.FromSeconds(3);

    // CPU counters
    private readonly PerformanceCounter? _procPerf;
    private readonly PerformanceCounter? _procPerformance;
    private readonly PerformanceCounter? _procFrequency;

    // Per-processor utilization and the busiest processes, only while a CPU card shows them.
    private readonly ProcessCpuSampler _processSampler = new();
    private bool _cpuCounterReadFailureLogged;
    private PerformanceCounterCategory? _processorCategory;
    private bool _processorCategoryCreated;
    private Dictionary<string, CounterSample> _previousCoreSamples = [];
    private long _previousCoreTimestamp;
    private int _detailRequests;
    private bool _detailsRead;
    private bool _coreReadFailureLogged;
    private bool _processReadFailureLogged;

    public float CpuUsage { get; set; }

    public float CpuSpeed { get; set; }

    /// <summary>
    /// Gets or sets the processor's base speed in MHz, as Task Manager shows it: the rated speed of
    /// its fastest cores.
    /// </summary>
    public float CpuBaseSpeed { get; set; }

    public uint ProcessCount { get; private set; }

    public uint ThreadCount { get; private set; }

    public uint HandleCount { get; private set; }

    public static string ProcessorName => CachedProcessorName.Value;

    public static int LogicalProcessorCount => Environment.ProcessorCount;

    public static TimeSpan Uptime => TimeSpan.FromMilliseconds(Environment.TickCount64);

    public List<float> CpuChartValues { get; set; } = new();

    /// <summary>Gets each logical processor's utilization in percent; empty unless a card requested details.</summary>
    public float[] CoreUsage { get; private set; } = [];

    /// <summary>Gets the busiest processes, busiest first. It's empty unless a card has requested details.</summary>
    public ProcessCpuSampler.ProcessUsage[] TopProcesses { get; private set; } = [];

    public CPUStats()
    {
        CpuUsage = 0;
        CpuBaseSpeed = CachedBaseSpeed.Value;

        // Use "% Processor Time" instead of "% Processor Utility": the latter is unbounded above 100%
        // when cores boost above their nominal base frequency (it is scaled by % Processor Performance),
        // which produced values like 144% in the dock under heavy load. % Processor Time is the same
        // counter Task Manager renders and is naturally bounded to 0-100%. See issue #46381.
        _procPerf = CreatePerformanceCounter("Processor Information", "% Processor Time", "_Total");
        _procPerformance = CreatePerformanceCounter("Processor Information", "% Processor Performance", "_Total");
        _procFrequency = CreatePerformanceCounter("Processor Information", "Processor Frequency", "_Total");
    }

    public void GetData()
    {
        try
        {
            if (_procPerf is not null)
            {
                CpuUsage = _procPerf.NextValue() / 100;
            }

            if (_procFrequency is not null && _procPerformance is not null)
            {
                var frequency = _procFrequency.NextValue();
                if (CachedBaseSpeed.Value == 0)
                {
                    // Without the rated speeds, use the counter's frequency. It's the same on chips
                    // with one core cluster.
                    CpuBaseSpeed = frequency;
                }

                CpuSpeed = frequency * (_procPerformance.NextValue() / 100);
            }

            ReadSystemCounts();

            if (Volatile.Read(ref _detailRequests) > 0)
            {
                _detailsRead = true;
                ReadCoreUsage();
                ReadTopProcesses();
            }
            else if (_detailsRead)
            {
                _detailsRead = false;
                CoreUsage = [];
                TopProcesses = [];
                _previousCoreSamples = [];
                _processSampler.Reset();
            }

            lock (CpuChartValues)
            {
                ChartHelper.AddNextChartValue(CpuUsage * 100, CpuChartValues, PerformanceChartData.HistoryLength);
            }
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
    /// Starts or stops reading per-processor utilization and the busiest processes. Only the CPU
    /// card shows them, so the dock and the overview don't pay for them.
    /// </summary>
    internal void RequestDetails(bool request)
    {
        if (request)
        {
            Interlocked.Increment(ref _detailRequests);
        }
        else if (Interlocked.Decrement(ref _detailRequests) < 0)
        {
            Interlocked.Exchange(ref _detailRequests, 0);
        }
    }

    private void ReadTopProcesses()
    {
        try
        {
            TopProcesses = _processSampler.Sample(5);
        }
        catch (Exception ex)
        {
            LogFailureOnce(ref _processReadFailureLogged, "Failed while reading process CPU times.", ex);
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

            var timestamp = Stopwatch.GetTimestamp();
            var samples = new Dictionary<string, CounterSample>();
            foreach (InstanceData instance in categoryData["% Processor Time"].Values)
            {
                if (TryParseProcessor(instance.InstanceName, out _, out _))
                {
                    samples[instance.InstanceName] = instance.Sample;
                }
            }

            var previous = _previousCoreSamples;
            if (Stopwatch.GetElapsedTime(_previousCoreTimestamp, timestamp) > CoreSampleMaximumGap)
            {
                previous = [];
            }

            _previousCoreSamples = samples;
            _previousCoreTimestamp = timestamp;
            CoreUsage = ComputeCoreUsage(previous, samples);
        }
        catch (Exception ex)
        {
            LogFailureOnce(ref _coreReadFailureLogged, "Failed while reading per-processor performance counters.", ex);
        }
    }

    /// <summary>
    /// Computes each processor's utilization in percent from two samples per processor, or nothing
    /// until every processor has an earlier sample.
    /// </summary>
    internal static float[] ComputeCoreUsage(
        IReadOnlyDictionary<string, CounterSample> previous,
        IReadOnlyDictionary<string, CounterSample> current)
    {
        var cores = new List<(int Group, int Index, float Usage)>(current.Count);
        foreach (var (name, sample) in current)
        {
            if (!TryParseProcessor(name, out var group, out var index))
            {
                continue;
            }

            if (!previous.TryGetValue(name, out var earlier))
            {
                return [];
            }

            var cooked = CounterSampleCalculator.ComputeCounterValue(earlier, sample);
            cores.Add((group, index, float.IsFinite(cooked) ? Math.Clamp(cooked, 0f, 100f) : 0f));
        }

        cores.Sort(static (left, right) => left.Group != right.Group ? left.Group.CompareTo(right.Group) : left.Index.CompareTo(right.Index));
        return cores.Select(static core => core.Usage).ToArray();
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

    /// <summary>
    /// Reads the processor's base speed in MHz, or 0 if it can't be read. Chips with more than one
    /// core cluster rate each cluster differently, and Task Manager shows the highest rating.
    /// </summary>
    private static unsafe uint ReadBaseSpeed()
    {
        try
        {
            // The call fails unless the buffer has room for every active processor.
            var count = PInvoke.GetActiveProcessorCount(PInvoke.ALL_PROCESSOR_GROUPS);
            if (count == 0)
            {
                return 0;
            }

            var processors = new PROCESSOR_POWER_INFORMATION[count];
            fixed (PROCESSOR_POWER_INFORMATION* buffer = processors)
            {
                var status = PInvoke.CallNtPowerInformation(
                    POWER_INFORMATION_LEVEL.ProcessorInformation,
                    null,
                    0,
                    buffer,
                    (uint)(processors.Length * sizeof(PROCESSOR_POWER_INFORMATION)));
                if (status.Value != 0)
                {
                    return 0;
                }
            }

            return GetBaseSpeed(processors);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    /// <summary>Returns the highest rated speed of the processors in MHz, or 0 if none is known.</summary>
    internal static uint GetBaseSpeed(ReadOnlySpan<PROCESSOR_POWER_INFORMATION> processors)
    {
        uint fastest = 0;
        foreach (var processor in processors)
        {
            fastest = Math.Max(fastest, processor.MaxMhz);
        }

        return fastest;
    }

    public void Dispose()
    {
        _procPerf?.Dispose();
        _procPerformance?.Dispose();
        _procFrequency?.Dispose();
    }
}
