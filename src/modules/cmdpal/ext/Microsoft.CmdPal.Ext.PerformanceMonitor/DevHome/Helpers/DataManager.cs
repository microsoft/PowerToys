// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.CmdPal.Ext.PerformanceMonitor;
using Timer = System.Timers.Timer;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class DataManager : IDisposable
{
    // Every page that shows a metric runs its own timer, and the dock bands and the list page
    // can show the same metric at the same time. They share one stats object, so sample it at
    // most once per interval: its history must keep one sample per second. The interval leaves
    // room for timer jitter.
    internal const long MinimumSampleIntervalMilliseconds = 750;

    private static readonly ConditionalWeakTable<object, StrongBox<long>> LastSampleTimes = new();

    private readonly SystemData _systemData = SystemData.Shared;
    private readonly DataType _dataType;
    private readonly Timer _updateTimer;
    private readonly Action _updateAction;
    private bool _updateFailureLogged;

    private const int OneSecondInMilliseconds = 1000;

    public DataManager(DataType type, Action updateWidget)
    {
        _updateAction = updateWidget;
        _dataType = type;

        _updateTimer = new Timer(OneSecondInMilliseconds);
        _updateTimer.Elapsed += UpdateTimer_Elapsed;
        _updateTimer.AutoReset = true;
        _updateTimer.Enabled = false;
    }

    private void GetMemoryData()
    {
        lock (_systemData.MemoryStats)
        {
            if (ShouldSample(_systemData.MemoryStats))
            {
                _systemData.MemoryStats.GetData();
            }
        }
    }

    private void GetNetworkData()
    {
        lock (_systemData.NetworkStats)
        {
            if (ShouldSample(_systemData.NetworkStats))
            {
                _systemData.NetworkStats.GetData();
            }
        }
    }

    private void GetDiskData()
    {
        lock (_systemData.DiskStats)
        {
            if (ShouldSample(_systemData.DiskStats))
            {
                _systemData.DiskStats.GetData();
            }
        }
    }

    private void GetGPUData()
    {
        lock (_systemData.GPUStats)
        {
            if (ShouldSample(_systemData.GPUStats))
            {
                _systemData.GPUStats.GetData();
            }
        }
    }

    private void GetCPUData(bool includeTopProcesses)
    {
        lock (_systemData.CpuStats)
        {
            if (ShouldSample(_systemData.CpuStats))
            {
                _systemData.CpuStats.GetData(includeTopProcesses);
            }
        }
    }

    private void GetBatteryData()
    {
        lock (_systemData.BatteryStats)
        {
            if (ShouldSample(_systemData.BatteryStats))
            {
                _systemData.BatteryStats.GetData();
            }
        }
    }

    /// <summary>
    /// Returns whether <paramref name="stats"/> is due for a sample, and if so records
    /// <paramref name="nowMilliseconds"/> as its sample time. Call it under the stats lock.
    /// </summary>
    internal static bool ShouldSample(object stats, long nowMilliseconds)
    {
        var lastSample = LastSampleTimes.GetValue(stats, static _ => new StrongBox<long>(long.MinValue));
        if (lastSample.Value != long.MinValue && nowMilliseconds - lastSample.Value < MinimumSampleIntervalMilliseconds)
        {
            return false;
        }

        lastSample.Value = nowMilliseconds;
        return true;
    }

    private static bool ShouldSample(object stats) => ShouldSample(stats, Environment.TickCount64);

    private void UpdateTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        var firstUpdateBlockSuffix = GetFirstUpdateBlockSuffix();
        var isTracked = firstUpdateBlockSuffix is not null && PerformanceMonitorCommandsProvider.CrashSentinel.BeginBlock(firstUpdateBlockSuffix);

        try
        {
            switch (_dataType)
            {
                case DataType.CPU:
                case DataType.CpuWithTopProcesses:
                    {
                        // CPU
                        GetCPUData(_dataType == DataType.CpuWithTopProcesses);
                        break;
                    }

                case DataType.GPU:
                    {
                        // gpu
                        GetGPUData();
                        break;
                    }

                case DataType.Memory:
                    {
                        // memory
                        GetMemoryData();
                        break;
                    }

                case DataType.Network:
                    {
                        // network
                        GetNetworkData();
                        break;
                    }

                case DataType.Disk:
                    {
                        // disk
                        GetDiskData();
                        break;
                    }

                case DataType.Battery:
                    {
                        GetBatteryData();
                        break;
                    }
            }

            if (isTracked)
            {
                PerformanceMonitorCommandsProvider.CrashSentinel.CompleteBlock(firstUpdateBlockSuffix!);
            }

            _updateAction?.Invoke();
        }
        catch (Exception ex)
        {
            if (isTracked)
            {
                PerformanceMonitorCommandsProvider.CrashSentinel.CancelBlock(firstUpdateBlockSuffix!);
            }

            _updateTimer.Stop();
            if (!_updateFailureLogged)
            {
                _updateFailureLogged = true;
                Microsoft.CmdPal.Common.CoreLogger.LogError($"Unexpected exception while updating performance monitor data for {_dataType}. Timer stopped.", ex);
            }
        }
    }

    private string? GetFirstUpdateBlockSuffix()
    {
        return _dataType switch
        {
            DataType.CPU => "CPU.FirstUpdate",
            DataType.CpuWithTopProcesses => "CPU.FirstUpdate",
            DataType.GPU => "GPU.FirstUpdate",
            DataType.Memory => "Memory.FirstUpdate",
            DataType.Network => "Network.FirstUpdate",
            DataType.Disk => "Disk.FirstUpdate",
            DataType.Battery => "Battery.FirstUpdate",
            _ => null,
        };
    }

    internal MemoryStats GetMemoryStats()
    {
        lock (_systemData.MemoryStats)
        {
            return _systemData.MemoryStats;
        }
    }

    internal NetworkStats GetNetworkStats()
    {
        lock (_systemData.NetworkStats)
        {
            return _systemData.NetworkStats;
        }
    }

    internal DiskStats GetDiskStats()
    {
        lock (_systemData.DiskStats)
        {
            return _systemData.DiskStats;
        }
    }

    internal GPUStats GetGPUStats()
    {
        lock (_systemData.GPUStats)
        {
            return _systemData.GPUStats;
        }
    }

    internal CPUStats GetCPUStats()
    {
        lock (_systemData.CpuStats)
        {
            return _systemData.CpuStats;
        }
    }

    internal BatteryStats GetBatteryStats()
    {
        lock (_systemData.BatteryStats)
        {
            return _systemData.BatteryStats;
        }
    }

    public void Start()
    {
        _updateTimer.Start();

        // Show current values as soon as the page opens, instead of after the first interval.
        ThreadPool.QueueUserWorkItem(static manager => manager.UpdateTimer_Elapsed(null, null!), this, preferLocal: false);
    }

    public void Stop()
    {
        _updateTimer.Stop();
    }

    public void Dispose()
    {
        _updateTimer.Dispose();
    }
}
