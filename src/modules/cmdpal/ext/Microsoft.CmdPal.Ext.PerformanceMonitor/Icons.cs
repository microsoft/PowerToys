// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

internal sealed class Icons
{
    internal static IconInfo CpuIcon => new("\uE9D9"); // CPU icon

    internal static IconInfo MemoryIcon => new("\uE964"); // Memory icon

    internal static IconInfo DiskIcon => new("\uE977"); // PC1 icon

    internal static IconInfo HardDriveIcon => new("\uEDA2"); // HardDrive icon

    internal static IconInfo NetworkIcon => new("\uEC05"); // Network icon

    internal static IconInfo StackedAreaIcon => new("\uE9D2"); // StackedArea icon

    // TODO! make different
    internal static IconInfo GpuIcon => new("\uE9D9"); // CPU icon

    internal static IconInfo NavigateBackwardIcon => new("\uE72B"); // Previous icon

    internal static IconInfo NavigateForwardIcon => new("\uE72A"); // Next icon
}


#pragma warning restore SA1402 // File may only contain a single type
