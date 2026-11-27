// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.NetworkInformation;

namespace CoreWidgetProvider.Helpers;

/// <summary>
/// Captures interface metadata, cumulative byte counters, and link speed in bits per second (zero when unavailable).
/// </summary>
/// <param name="InterfaceLuid">Locally unique interface identifier used to correlate counter samples.</param>
/// <param name="InterfaceGuid">Interface GUID used for persisted adapter selection.</param>
/// <param name="Name">Display name, preferring the alias over the description or GUID.</param>
/// <param name="ReceivedBytes">Cumulative received bytes.</param>
/// <param name="SentBytes">Cumulative transmitted bytes.</param>
/// <param name="LinkSpeed">Maximum known receive or transmit speed in bits per second; zero if unknown or inactive.</param>
/// <param name="IsHardware">Native <c>HardwareInterface</c> flag used for filtering and physical-adapter totals.</param>
internal readonly record struct NetworkInterfaceSnapshot(
    ulong InterfaceLuid,
    Guid InterfaceGuid,
    string Name,
    ulong ReceivedBytes,
    ulong SentBytes,
    ulong LinkSpeed,
    bool IsHardware)
{
    /// <summary>
    /// Gets the driver-provided interface description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the native interface type, including values not named in <see cref="NetworkInterfaceType"/>.
    /// </summary>
    public NetworkInterfaceType Type { get; init; } = NetworkInterfaceType.Unknown;

    /// <summary>
    /// Gets the native operational status.
    /// </summary>
    public OperationalStatus Status { get; init; } = OperationalStatus.Unknown;
}
