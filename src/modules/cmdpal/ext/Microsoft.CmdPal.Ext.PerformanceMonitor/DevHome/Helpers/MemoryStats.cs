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
    private readonly PerformanceCounter? _memModified;
    private readonly PerformanceCounter? _memFree;
    private readonly ulong? _installedMemoryBytes;
    private bool _memoryCounterReadFailureLogged;
    private bool _pageListReadFailureLogged;

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

    public ulong? InstalledMem { get; private set; }

    public ulong? ModifiedMem { get; private set; }

    public ulong? FreeMem { get; private set; }

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
        _memModified = CreatePerformanceCounter("Memory", "Modified Page List Bytes");
        _memFree = CreatePerformanceCounter("Memory", "Free & Zero Page List Bytes");
        if (PInvoke.GetPhysicallyInstalledSystemMemory(out var installedKilobytes) && installedKilobytes <= ulong.MaxValue / 1024)
        {
            _installedMemoryBytes = installedKilobytes * 1024;
        }
    }

    public void GetData()
    {
        Windows.Win32.System.SystemInformation.MEMORYSTATUSEX memStatus = default;
        memStatus.dwLength = (uint)Marshal.SizeOf<Windows.Win32.System.SystemInformation.MEMORYSTATUSEX>();
        if (PInvoke.GlobalMemoryStatusEx(ref memStatus))
        {
            ulong? modifiedBytes = null;
            ulong? freeBytes = null;
            try
            {
                // Read the page-list sizes as integers, without float precision loss.
                var modified = _memModified?.RawValue;
                var free = _memFree?.RawValue;
                if (modified is >= 0 && free is >= 0)
                {
                    modifiedBytes = (ulong)modified.Value;
                    freeBytes = (ulong)free.Value;
                }
            }
            catch (Exception ex)
            {
                LogFailureOnce(ref _pageListReadFailureLogged, "Failed while reading memory page lists.", ex);
            }

            ApplyPhysicalMemory(memStatus.ullTotalPhys, memStatus.ullAvailPhys, _installedMemoryBytes, modifiedBytes, freeBytes);
        }

        try
        {
            if (!ModifiedMem.HasValue || !FreeMem.HasValue)
            {
                MemCached = (ulong)(_memCached?.NextValue() ?? 0);
            }

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

    internal void ApplyPhysicalMemory(ulong totalBytes, ulong availableBytes, ulong? installedBytes = null, ulong? modifiedBytes = null, ulong? freeBytes = null)
    {
        if (totalBytes == 0 || availableBytes > totalBytes)
        {
            return;
        }

        AllMem = totalBytes;
        AvailableMem = availableBytes;
        UsedMem = totalBytes - availableBytes;
        InstalledMem = installedBytes >= totalBytes ? installedBytes : null;

        // The OS counters are read separately. Bound page lists to their physical
        // partitions so a changing sample cannot overlap or exceed the total.
        var hasPageLists = modifiedBytes.HasValue && freeBytes.HasValue;
        ModifiedMem = hasPageLists ? Math.Min(modifiedBytes!.Value, UsedMem) : null;
        FreeMem = hasPageLists ? Math.Min(freeBytes!.Value, AvailableMem) : null;
        if (hasPageLists)
        {
            MemCached = ModifiedMem!.Value + (AvailableMem - FreeMem!.Value);
        }

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
        _memModified?.Dispose();
        _memFree?.Dispose();
    }
}
