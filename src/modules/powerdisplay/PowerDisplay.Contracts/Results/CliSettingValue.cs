// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Contracts;

public sealed class CliSettingValue
{
    public string Setting { get; init; } = string.Empty;

    /// <summary>
    /// Gets the machine-readable current value, or <c>null</c> when the monitor does not support
    /// the setting or discovery did not read it — so a default/stale field is never reported as a
    /// live value. Omitted from JSON when null.
    /// </summary>
    public int? Raw { get; init; }

    /// <summary>
    /// Gets the human-readable current value, or <c>null</c> under the same conditions as
    /// <see cref="Raw"/>. Omitted from JSON when null.
    /// </summary>
    public string? Display { get; init; }

    public bool Supported { get; init; }
}
