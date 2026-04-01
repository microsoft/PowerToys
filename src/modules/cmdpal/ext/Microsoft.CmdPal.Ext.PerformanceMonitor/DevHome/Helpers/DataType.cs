// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CoreWidgetProvider.Helpers;

public enum DataType
{
    /// <summary>
    /// CPU related data.
    /// </summary>
    CPU,

    /// <summary>
    /// CPU related data, including the top processes.
    /// Calculating the top processes takes a lot longer,
    /// so by default we don't.
    /// </summary>
    CpuWithTopProcesses,

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
}
