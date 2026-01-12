// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Timer = System.Timers.Timer;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class DataManager : IDisposable
{
    private readonly SystemData _systemData;
    private readonly DataType _dataType;
    private readonly Timer _updateTimer;
    private readonly Action _updateAction;

    private const int OneSecondInMilliseconds = 1000;

    public DataManager(DataType type, Action updateWidget)
    {
        _systemData = new SystemData();
        _updateAction = updateWidget;
        _dataType = type;

        _updateTimer = new Timer(OneSecondInMilliseconds);
        _updateTimer.Elapsed += UpdateTimer_Elapsed;
        _updateTimer.AutoReset = true;
        _updateTimer.Enabled = false;
    }

    private void GetMemoryData()
    {
        lock (SystemData.MemStats)
        {
            SystemData.MemStats.GetData();
        }
    }

    private void GetNetworkData()
    {
        lock (SystemData.NetStats)
        {
            SystemData.NetStats.GetData();
        }
    }

    private void GetGPUData()
    {
        lock (SystemData.GPUStats)
        {
            SystemData.GPUStats.GetData();
        }
    }

    private void GetCPUData()
    {
        lock (SystemData.CpuStats)
        {
            SystemData.CpuStats.GetData();
        }
    }

    private void UpdateTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        switch (_dataType)
        {
            case DataType.CPU:
                {
                    // CPU
                    GetCPUData();
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

    internal MemoryStats GetMemoryStats()
    {
        lock (SystemData.MemStats)
        {
            return SystemData.MemStats;
        }
    }

    internal NetworkStats GetNetworkStats()
    {
        lock (SystemData.NetStats)
        {
            return SystemData.NetStats;
        }
    }

    internal GPUStats GetGPUStats()
    {
        lock (SystemData.GPUStats)
        {
            return SystemData.GPUStats;
        }
    }

    internal CPUStats GetCPUStats()
    {
        lock (SystemData.CpuStats)
        {
            return SystemData.CpuStats;
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
        _systemData.Dispose();
        _updateTimer.Dispose();
    }
}
