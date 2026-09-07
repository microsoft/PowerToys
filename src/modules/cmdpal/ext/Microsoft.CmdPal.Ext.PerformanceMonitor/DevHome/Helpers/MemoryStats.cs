// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class MemoryStats : PerformanceCounterSourceBase, IDisposable
{
    private const double BytesPerGigabyte = 1024d * 1024 * 1024;

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

    public ulong AvailableMem { get; private set; }

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

    public UsageHistory MemoryHistory { get; }

    public MemoryStats(TimeProvider? timeProvider = null)
    {
        MemoryHistory = new(seriesCount: 3, timeProvider);
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
            ApplyPhysicalMemory(memStatus.ullTotalPhys, memStatus.ullAvailPhys);
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

    internal void ApplyPhysicalMemory(ulong totalBytes, ulong availableBytes)
    {
        if (totalBytes == 0 || availableBytes > totalBytes)
        {
            return;
        }

        AllMem = totalBytes;
        AvailableMem = availableBytes;
        UsedMem = totalBytes - availableBytes;
        var usage = (double)UsedMem / totalBytes;
        MemUsage = (float)usage;

        // Keep the percentage and GB readouts at the same observation time.
        MemoryHistory.Add(usage * 100, UsedMem / BytesPerGigabyte, AvailableMem / BytesPerGigabyte);
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
