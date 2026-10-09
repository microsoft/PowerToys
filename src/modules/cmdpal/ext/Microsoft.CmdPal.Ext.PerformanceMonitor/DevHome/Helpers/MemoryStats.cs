// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class MemoryStats : PerformanceCounterSourceBase, IDisposable
{
    private readonly PerformanceCounter? _memCommitted;
    private readonly PerformanceCounter? _memCached;
    private readonly PerformanceCounter? _memCommittedLimit;
    private readonly PerformanceCounter? _memPoolPaged;
    private readonly PerformanceCounter? _memPoolNonPaged;
    private bool _memoryCounterReadFailureLogged;

    public float MemUsage
    {
        get; set;
    }

    public ulong AllMem
    {
        get; set;
    }

    public ulong UsedMem
    {
        get; set;
    }

    public ulong MemCommitted
    {
        get; set;
    }

    public ulong MemCommitLimit
    {
        get; set;
    }

    public ulong MemCached
    {
        get; set;
    }

    public ulong MemPagedPool
    {
        get; set;
    }

    public ulong MemNonPagedPool
    {
        get; set;
    }

    public List<float> MemChartValues { get; set; } = new();

    public MemoryStats()
    {
        _memCommitted = CreatePerformanceCounter("Memory", "Committed Bytes");
        _memCached = CreatePerformanceCounter("Memory", "Cache Bytes");
        _memCommittedLimit = CreatePerformanceCounter("Memory", "Commit Limit");
        _memPoolPaged = CreatePerformanceCounter("Memory", "Pool Paged Bytes");
        _memPoolNonPaged = CreatePerformanceCounter("Memory", "Pool Nonpaged Bytes");
    }

    public void GetData()
    {
        Windows.Win32.System.SystemInformation.MEMORYSTATUSEX memStatus = default;
        memStatus.dwLength = (uint)Marshal.SizeOf<Windows.Win32.System.SystemInformation.MEMORYSTATUSEX>();
        if (PInvoke.GlobalMemoryStatusEx(ref memStatus))
        {
            AllMem = memStatus.ullTotalPhys;
            var availableMem = memStatus.ullAvailPhys;
            UsedMem = AllMem - availableMem;

            MemUsage = (float)UsedMem / AllMem;
            lock (MemChartValues)
            {
                ChartHelper.AddNextChartValue(MemUsage * 100, MemChartValues);
            }
        }

        try
        {
            MemCached = (ulong)(_memCached?.NextValue() ?? 0);
            MemCommitted = (ulong)(_memCommitted?.NextValue() ?? 0);
            MemCommitLimit = (ulong)(_memCommittedLimit?.NextValue() ?? 0);
            MemPagedPool = (ulong)(_memPoolPaged?.NextValue() ?? 0);
            MemNonPagedPool = (ulong)(_memPoolNonPaged?.NextValue() ?? 0);
        }
        catch (Exception ex)
        {
            LogFailureOnce(ref _memoryCounterReadFailureLogged, "Failed while reading memory performance counters.", ex);
        }
    }

    public string CreateMemImageUrl()
    {
        return ChartHelper.CreateImageUrl(MemChartValues, ChartHelper.ChartType.Mem);
    }

    public void Dispose()
    {
        _memCommitted?.Dispose();
        _memCached?.Dispose();
        _memCommittedLimit?.Dispose();
        _memPoolPaged?.Dispose();
        _memPoolNonPaged?.Dispose();
    }
}
