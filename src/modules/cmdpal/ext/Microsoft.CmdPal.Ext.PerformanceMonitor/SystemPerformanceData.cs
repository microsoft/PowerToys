// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

/// <summary>
/// Class for storing all performance metrics in one place
/// </summary>
internal sealed class SystemPerformanceData
{
    // Process information
    public string TopCpuProcesses { get; set; } = "Loading process data...";

    public string TopMemoryProcesses { get; set; } = "Loading process data...";

    public string TopDiskProcesses { get; set; } = "Loading process data...";

    public string TopNetworkProcesses { get; set; } = "Loading process data...";

    // Current values
    public float CurrentCpuUsage { get; set; }

    public float CurrentMemoryUsage { get; set; }

    public float AvailableMemoryMB { get; set; }

    public float CurrentDiskUsage { get; set; }

    public float CurrentNetworkSentKBps { get; set; }

    public float CurrentNetworkReceivedKBps { get; set; }

    // System information
    public string ProcessorName { get; set; } = string.Empty;

    public string DiskInformation { get; set; } = string.Empty;

    public string NetworkInformation { get; set; } = string.Empty;
}
