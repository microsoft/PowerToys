// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CmdPal.Ext.PerformanceMonitor;
using Timer = System.Timers.Timer;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class DataManager : IDisposable
{
    private readonly SystemData _systemData = SystemData.Shared;
    private readonly DataType _dataType;
    private readonly Timer? _updateTimer;
    private readonly CpuSampler? _cpuSampler;
    private readonly NetworkStats? _networkStats;
    private readonly MemoryStats? _memoryStats;
    private readonly Lock _lifecycleLock = new();
    private readonly Action _updateAction;
    private IDisposable? _samplingSubscription;
    private bool _disposed;
    private bool _updateFailureLogged;

    private const int OneSecondInMilliseconds = 1000;

    public DataManager(DataType type, Action updateWidget, CpuSampler? cpuSampler = null, NetworkStats? networkStats = null, MemoryStats? memoryStats = null)
    {
        _updateAction = updateWidget;
        _dataType = type;
        _networkStats = networkStats;
        _memoryStats = memoryStats;

        if (type is DataType.CPU or DataType.CpuWithTopProcesses)
        {
            _cpuSampler = cpuSampler ?? _systemData.CpuSampler;
            return;
        }

        if (type == DataType.Network)
        {
            // The counter source owns a single polling timer for every network view.
            return;
        }

        _updateTimer = new Timer(OneSecondInMilliseconds);
        _updateTimer.Elapsed += UpdateTimer_Elapsed;
        _updateTimer.AutoReset = true;
        _updateTimer.Enabled = false;
    }

    private void GetMemoryData()
    {
        var stats = GetMemoryStats();
        lock (stats)
        {
            stats.GetData();
        }
    }

    private void GetDiskData()
    {
        lock (_systemData.DiskStats)
        {
            _systemData.DiskStats.GetData();
        }
    }

    private void GetGPUData()
    {
        lock (_systemData.GPUStats)
        {
            _systemData.GPUStats.GetData();
        }
    }

    private void GetBatteryData()
    {
        lock (_systemData.BatteryStats)
        {
            _systemData.BatteryStats.GetData();
        }
    }

    private void UpdateTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        var firstUpdateBlockSuffix = GetFirstUpdateBlockSuffix();
        var isTracked = firstUpdateBlockSuffix is not null && PerformanceMonitorCommandsProvider.CrashSentinel.BeginBlock(firstUpdateBlockSuffix);

        try
        {
            switch (_dataType)
            {
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

            _updateTimer!.Stop();
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
            DataType.GPU => "GPU.FirstUpdate",
            DataType.Memory => "Memory.FirstUpdate",
            DataType.Disk => "Disk.FirstUpdate",
            DataType.Battery => "Battery.FirstUpdate",
            _ => null,
        };
    }

    internal MemoryStats GetMemoryStats() => _memoryStats ?? _systemData.MemoryStats;

    internal NetworkStats GetNetworkStats() => _networkStats ?? _systemData.NetworkStats;

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

    internal CpuSnapshot GetCpuSnapshot()
        => (_cpuSampler ?? throw new InvalidOperationException("This data manager does not monitor CPU usage.")).Snapshot;

    internal BatteryStats GetBatteryStats()
    {
        lock (_systemData.BatteryStats)
        {
            return _systemData.BatteryStats;
        }
    }

    public void Start()
    {
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_cpuSampler is not null)
            {
                _samplingSubscription ??= _cpuSampler.Subscribe(_updateAction, _dataType == DataType.CpuWithTopProcesses);
            }
            else if (_dataType == DataType.Network)
            {
                _samplingSubscription ??= GetNetworkStats().Sampler.Subscribe(_updateAction);
            }
            else
            {
                _updateTimer!.Start();
            }
        }
    }

    public void Stop()
    {
        lock (_lifecycleLock)
        {
            _samplingSubscription?.Dispose();
            _samplingSubscription = null;
            _updateTimer?.Stop();
        }
    }

    public void Dispose()
    {
        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _samplingSubscription?.Dispose();
            _samplingSubscription = null;
            _updateTimer?.Dispose();
        }
    }
}
