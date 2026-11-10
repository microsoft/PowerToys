// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
namespace PowerDisplay.Contracts;

/// <summary>
/// Request for the relative <c>up</c>/<c>down</c> commands. The direction is carried by
/// <see cref="CliRequestEnvelope.Command"/> ("up" or "down"); this payload names the target
/// continuous setting and an optional step.
/// </summary>
public sealed class AdjustRequest
{
    public int? MonitorNumber { get; set; }

    public string? MonitorId { get; set; }

    /// <summary>One of the continuous setting names: brightness, contrast, volume.</summary>
    public string Setting { get; set; } = string.Empty;

    /// <summary>Step amount; <see langword="null"/> means "use the mouse_wheel_increment setting".</summary>
    public int? Step { get; set; }
}
