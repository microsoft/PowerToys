// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Timer = System.Timers.Timer;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class DataManager : IDisposable
{
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
            _systemData.MemoryStats.GetData();
        }
    }

    private void GetNetworkData()
    {
        lock (_systemData.NetworkStats)
        {
            _systemData.NetworkStats.GetData();
        }
    }

    private void GetGPUData()
    {
        lock (_systemData.GPUStats)
        {
            _systemData.GPUStats.GetData();
        }
    }

    private void GetCPUData(bool includeTopProcesses)
    {
        lock (_systemData.CpuStats)
        {
            _systemData.CpuStats.GetData(includeTopProcesses);
        }
    }

    private void UpdateTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
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
            }

            _updateAction?.Invoke();
        }
        catch (Exception ex)
        {
            _updateTimer.Stop();
            if (!_updateFailureLogged)
            {
                _updateFailureLogged = true;
                Microsoft.CmdPal.Common.CoreLogger.LogError($"Unexpected exception while updating performance monitor data for {_dataType}. Timer stopped.", ex);
            }
        }
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

    public void Start()
    {
        _updateTimer.Start();
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
