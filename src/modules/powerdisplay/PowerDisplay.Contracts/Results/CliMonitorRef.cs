// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Contracts;

/// <summary>
/// Compact identification of a monitor used inside every JSON response so
/// consumers can correlate the result back to a single physical device.
/// </summary>
public sealed class CliMonitorRef
{
    public int Number { get; init; }

    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Communication transport (<c>DDC/CI</c> for external monitors, <c>WMI</c> for
    /// internal panels). Set on the <c>list</c>/<c>get</c>/<c>set</c> envelopes; left
    /// <c>null</c> (and omitted from JSON) by <c>capabilities</c>, which carries the
    /// transport in its dedicated top-level <c>communicationMethod</c> field instead.
    /// </summary>
    public string? Method { get; init; }
}
