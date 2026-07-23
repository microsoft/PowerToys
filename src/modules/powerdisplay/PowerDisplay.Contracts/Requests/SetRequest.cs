// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
namespace PowerDisplay.Contracts;

public sealed class SetRequest
{
    public int? MonitorNumber { get; set; }

    public string? MonitorId { get; set; }

    /// <summary>One of the canonical setting names: brightness, contrast, volume,
    /// color-temperature, input-source, power-state, orientation.</summary>
    public string Setting { get; set; } = string.Empty;

    /// <summary>Raw user-supplied value; the app parses/validates against capabilities.</summary>
    public string RawValue { get; set; } = string.Empty;

    public bool ConfirmPowerOff { get; set; }
}
