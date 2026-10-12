// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.CmdPal.Ext.PerformanceMonitor;
using Windows.Win32;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class MemoryStats : PerformanceCounterSourceBase, IDisposable
{
    private readonly PerformanceCounter? _memCommitted;
    private readonly PerformanceCounter? _memCached;
    private readonly PerformanceCounter? _memCommittedLimit;
    private readonly PerformanceCounter? _memPoolPaged;
    private readonly PerformanceCounter? _memPoolNonPaged;
    private readonly PerformanceCounter? _memModified;
    private readonly PerformanceCounter? _memStandbyCore;
    private readonly PerformanceCounter? _memStandbyNormal;
    private readonly PerformanceCounter? _memStandbyReserve;
    private readonly PerformanceCounter? _memFree;
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

    /// <summary>Gets the memory that holds data waiting to be written to disk before reuse.</summary>
    public ulong MemModified { get; private set; }

    /// <summary>Gets cached memory that's available for immediate reuse.</summary>
    public ulong MemStandby { get; private set; }

    /// <summary>Gets memory that holds no data.</summary>
    public ulong MemFree { get; private set; }

    public ulong MemAvailable => AllMem - UsedMem;

    /// <summary>
    /// Gets memory in use by processes, drivers, and the operating system, excluding modified
    /// pages. In use, modified, standby, and free add up to the total, as in Task Manager.
    /// </summary>
    public ulong MemInUse => UsedMem > MemModified ? UsedMem - MemModified : UsedMem;

    public MemoryStats()
    {
        _memCommitted = CreatePerformanceCounter("Memory", "Committed Bytes");
        _memCached = CreatePerformanceCounter("Memory", "Cache Bytes");
        _memCommittedLimit = CreatePerformanceCounter("Memory", "Commit Limit");
        _memPoolPaged = CreatePerformanceCounter("Memory", "Pool Paged Bytes");
        _memPoolNonPaged = CreatePerformanceCounter("Memory", "Pool Nonpaged Bytes");
        _memModified = CreatePerformanceCounter("Memory", "Modified Page List Bytes");
        _memStandbyCore = CreatePerformanceCounter("Memory", "Standby Cache Core Bytes");
        _memStandbyNormal = CreatePerformanceCounter("Memory", "Standby Cache Normal Priority Bytes");
        _memStandbyReserve = CreatePerformanceCounter("Memory", "Standby Cache Reserve Bytes");
        _memFree = CreatePerformanceCounter("Memory", "Free & Zero Page List Bytes");
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
                ChartHelper.AddNextChartValue(MemUsage * 100, MemChartValues, PerformanceChartData.HistoryLength);
            }
        }

        try
        {
            MemCached = (ulong)(_memCached?.NextValue() ?? 0);
            MemCommitted = (ulong)(_memCommitted?.NextValue() ?? 0);
            MemCommitLimit = (ulong)(_memCommittedLimit?.NextValue() ?? 0);
            MemPagedPool = (ulong)(_memPoolPaged?.NextValue() ?? 0);
            MemNonPagedPool = (ulong)(_memPoolNonPaged?.NextValue() ?? 0);
            MemModified = (ulong)(_memModified?.NextValue() ?? 0);
            MemStandby = (ulong)((_memStandbyCore?.NextValue() ?? 0)
                + (_memStandbyNormal?.NextValue() ?? 0)
                + (_memStandbyReserve?.NextValue() ?? 0));
            MemFree = (ulong)(_memFree?.NextValue() ?? 0);
        }
        catch (Exception ex)
        {
            LogFailureOnce(ref _memoryCounterReadFailureLogged, "Failed while reading memory performance counters.", ex);
        }
    }

    public void Dispose()
    {
        _memCommitted?.Dispose();
        _memCached?.Dispose();
        _memCommittedLimit?.Dispose();
        _memPoolPaged?.Dispose();
        _memPoolNonPaged?.Dispose();
        _memModified?.Dispose();
        _memStandbyCore?.Dispose();
        _memStandbyNormal?.Dispose();
        _memStandbyReserve?.Dispose();
        _memFree?.Dispose();
    }
}
