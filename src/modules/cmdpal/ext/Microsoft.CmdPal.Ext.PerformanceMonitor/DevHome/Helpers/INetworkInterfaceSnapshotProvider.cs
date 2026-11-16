// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace CoreWidgetProvider.Helpers;

/// <summary>
/// Supplies network interface metadata and cumulative byte counters for rate calculation.
/// </summary>
internal interface INetworkInterfaceSnapshotProvider
{
    /// <summary>
    /// Reads the current metadata and cumulative counters for included interfaces.
    /// </summary>
    /// <returns>One snapshot per included interface, or an empty list if none are included.</returns>
    IReadOnlyList<NetworkInterfaceSnapshot> GetSnapshots();
}
