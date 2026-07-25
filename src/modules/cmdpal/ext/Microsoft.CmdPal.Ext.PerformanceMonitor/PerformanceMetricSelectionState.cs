// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

internal sealed class PerformanceMetricSelectionState
{
    private readonly object _networkSelectionLock = new();
    private int _diskIndex;
    private int _networkIndex;
    private int _gpuIndex;
    private bool _automaticallySelectNetwork = true;

    public int DiskIndex
    {
        get => Volatile.Read(ref _diskIndex);
        set => Volatile.Write(ref _diskIndex, value);
    }

    public int NetworkIndex
    {
        get
        {
            lock (_networkSelectionLock)
            {
                return _networkIndex;
            }
        }
    }

    public bool IsNetworkSelectionAutomatic
    {
        get
        {
            lock (_networkSelectionLock)
            {
                return _automaticallySelectNetwork;
            }
        }
    }

    public int UpdateAutomaticNetworkIndex(int value)
    {
        lock (_networkSelectionLock)
        {
            if (_automaticallySelectNetwork)
            {
                _networkIndex = value;
            }

            return _networkIndex;
        }
    }

    public bool SelectNetworkManually(int value)
    {
        lock (_networkSelectionLock)
        {
            if (_networkIndex == value)
            {
                return false;
            }

            _automaticallySelectNetwork = false;
            _networkIndex = value;
            return true;
        }
    }

    public int RecoverNetworkSelection(int expectedIndex, int replacementIndex)
    {
        lock (_networkSelectionLock)
        {
            if (_networkIndex == expectedIndex && replacementIndex != expectedIndex)
            {
                _automaticallySelectNetwork = true;
                _networkIndex = replacementIndex;
            }

            return _networkIndex;
        }
    }

    public int GpuIndex
    {
        get => Volatile.Read(ref _gpuIndex);
        set => Volatile.Write(ref _gpuIndex, value);
    }
}
