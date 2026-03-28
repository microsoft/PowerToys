// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.PerformanceMonitor;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class SystemData
{
    public static SystemData Shared { get; } = new();

    private readonly Lazy<MemoryStats> _memoryStats = new(() => CreateGuarded("Memory.Initialize", static () => new MemoryStats()));
    private readonly Lazy<NetworkStats> _networkStats = new(() => CreateGuarded("Network.Initialize", static () => new NetworkStats()));
    private readonly Lazy<GPUStats> _gpuStats = new(() => CreateGuarded("GPU.Initialize", static () => new GPUStats()));
    private readonly Lazy<CPUStats> _cpuStats = new(() => CreateGuarded("CPU.Initialize", static () => new CPUStats()));

    public MemoryStats MemoryStats => _memoryStats.Value;

    public NetworkStats NetworkStats => _networkStats.Value;

    public GPUStats GPUStats => _gpuStats.Value;

    public CPUStats CpuStats => _cpuStats.Value;

    private SystemData()
    {
    }

    private static T CreateGuarded<T>(string blockSuffix, Func<T> factory)
    {
        var isTracked = PerformanceMonitorCommandsProvider.CrashSentinel.BeginBlock(blockSuffix);

        try
        {
            var value = factory();
            if (isTracked)
            {
                PerformanceMonitorCommandsProvider.CrashSentinel.CompleteBlock(blockSuffix);
            }

            return value;
        }
        catch
        {
            if (isTracked)
            {
                PerformanceMonitorCommandsProvider.CrashSentinel.CancelBlock(blockSuffix);
            }

            throw;
        }
    }
}
