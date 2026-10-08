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

    // Per-processor utilization and the busiest processes, only while a CPU card shows them.
    private readonly ProcessCpuSampler _processSampler = new();
    private bool _cpuCounterReadFailureLogged;
    private PerformanceCounterCategory? _processorCategory;
    private bool _processorCategoryCreated;
    private Dictionary<string, CounterSample> _previousCoreSamples = [];
    private int _detailRequests;
    private bool _detailsRead;
    private bool _coreReadFailureLogged;
    private bool _processReadFailureLogged;

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

    public List<float> CpuChartValues { get; set; } = new();

    /// <summary>
    /// Gets the utilization of each logical processor in percent, in processor order. It's empty
    /// unless a card has requested details with <see cref="RequestDetails"/>.
    /// </summary>
    public float[] CoreUsage { get; private set; } = [];

    /// <summary>Gets the busiest processes, busiest first. It's empty unless a card has requested details.</summary>
    public ProcessCpuSampler.ProcessUsage[] TopProcesses { get; private set; } = [];

    public CPUStats()
    {
        CpuUsage = 0;

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
                CpuBaseSpeed = frequency;
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

    public void Dispose()
    {
        _procPerf?.Dispose();
        _procPerformance?.Dispose();
        _procFrequency?.Dispose();
    }
}
