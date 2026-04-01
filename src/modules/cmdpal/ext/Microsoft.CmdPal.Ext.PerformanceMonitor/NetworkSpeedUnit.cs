// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

/// <summary>
/// Controls the unit used to display network transmission speed.
/// </summary>
internal enum NetworkSpeedUnit
{
    /// <summary>Bits per second (Kbps, Mbps, Gbps) — SI decimal prefixes.</summary>
    BitsPerSecond,

    /// <summary>Bytes per second (KB/s, MB/s, GB/s) — SI decimal prefixes.</summary>
    BytesPerSecond,

    /// <summary>Bytes per second (KiB/s, MiB/s, GiB/s) — IEC binary prefixes.</summary>
    BinaryBytesPerSecond,
}
