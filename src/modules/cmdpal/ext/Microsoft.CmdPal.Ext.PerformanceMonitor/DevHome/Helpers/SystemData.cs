// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class SystemData
{
    public static SystemData Shared { get; } = new();

    private readonly Lazy<MemoryStats> _memoryStats = new(() => new MemoryStats());
    private readonly Lazy<NetworkStats> _networkStats = new(() => new NetworkStats());
    private readonly Lazy<GPUStats> _gpuStats = new(() => new GPUStats());
    private readonly Lazy<CPUStats> _cpuStats = new(() => new CPUStats());

    public MemoryStats MemoryStats => _memoryStats.Value;

    public NetworkStats NetworkStats => _networkStats.Value;

    public GPUStats GPUStats => _gpuStats.Value;

    public CPUStats CpuStats => _cpuStats.Value;

    private SystemData()
    {
    }
}
