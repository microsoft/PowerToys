// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

internal sealed class PerformanceMetricSelectionState
{
    private int _diskIndex;
    private int _networkIndex;
    private int _gpuIndex;

    public int DiskIndex
    {
        get => Volatile.Read(ref _diskIndex);
        set => Volatile.Write(ref _diskIndex, value);
    }

    public int NetworkIndex
    {
        get => Volatile.Read(ref _networkIndex);
        set => Volatile.Write(ref _networkIndex, value);
    }

    public int GpuIndex
    {
        get => Volatile.Read(ref _gpuIndex);
        set => Volatile.Write(ref _gpuIndex, value);
    }
}
