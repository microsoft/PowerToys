// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CoreWidgetProvider.Helpers;

public enum DataType
{
    /// <summary>
    /// CPU related data. The CPU card adds per-processor utilization and the busiest processes
    /// with <see cref="CPUStats.RequestDetails"/>.
    /// </summary>
    CPU,

    /// <summary>
    /// Memory related data.
    /// </summary>
    Memory,

    /// <summary>
    /// GPU related data.
    /// </summary>
    GPU,

    /// <summary>
    /// Network related data.
    /// </summary>
    Network,

    /// <summary>
    /// Battery related data.
    /// </summary>
    Battery,

    /// <summary>
    /// Disk related data.
    /// </summary>
    Disk,
}
