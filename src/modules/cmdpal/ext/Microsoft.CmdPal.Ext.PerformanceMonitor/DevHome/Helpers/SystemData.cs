// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class SystemData : IDisposable
{
    public static MemoryStats MemStats { get; set; } = new MemoryStats();

    public static NetworkStats NetStats { get; set; } = new NetworkStats();

    public static GPUStats GPUStats { get; set; } = new GPUStats();

    public static CPUStats CpuStats { get; set; } = new CPUStats();

    public SystemData()
    {
    }

    public void Dispose()
    {
    }
}
