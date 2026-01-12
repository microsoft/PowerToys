// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class MemoryStats : IDisposable
{
    private readonly PerformanceCounter _memCommitted = new("Memory", "Committed Bytes", string.Empty);
    private readonly PerformanceCounter _memCached = new("Memory", "Cache Bytes", string.Empty);
    private readonly PerformanceCounter _memCommittedLimit = new("Memory", "Commit Limit", string.Empty);
    private readonly PerformanceCounter _memPoolPaged = new("Memory", "Pool Paged Bytes", string.Empty);
    private readonly PerformanceCounter _memPoolNonPaged = new("Memory", "Pool Nonpaged Bytes", string.Empty);

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

    public void GetData()
    {
        // TODO!
        // Windows.Win32.System.SystemInformation.MEMORYSTATUSEX memStatus = default;
        // memStatus.dwLength = (uint)Marshal.SizeOf(typeof(Windows.Win32.System.SystemInformation.MEMORYSTATUSEX));
        // if (PInvoke.GlobalMemoryStatusEx(ref memStatus))
        // {
        //    AllMem = memStatus.ullTotalPhys;
        //    var availableMem = memStatus.ullAvailPhys;
        //    UsedMem = AllMem - availableMem;

        // MemUsage = (float)UsedMem / AllMem;
        //    lock (MemChartValues)
        //    {
        //        ChartHelper.AddNextChartValue(MemUsage * 100, MemChartValues);
        //    }
        // }
        MemCached = (ulong)_memCached.NextValue();
        MemCommitted = (ulong)_memCommitted.NextValue();
        MemCommitLimit = (ulong)_memCommittedLimit.NextValue();
        MemPagedPool = (ulong)_memPoolPaged.NextValue();
        MemNonPagedPool = (ulong)_memPoolNonPaged.NextValue();
    }

    public string CreateMemImageUrl()
    {
        return ChartHelper.CreateImageUrl(MemChartValues, ChartHelper.ChartType.Mem);
    }

    public void Dispose()
    {
        _memCommitted.Dispose();
        _memCached.Dispose();
        _memCommittedLimit.Dispose();
        _memPoolPaged.Dispose();
        _memPoolNonPaged.Dispose();
    }
}
