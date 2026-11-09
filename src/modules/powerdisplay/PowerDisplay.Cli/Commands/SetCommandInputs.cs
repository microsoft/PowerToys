// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Cli.Commands;

/// <summary>
/// Inputs collected from the parsed <c>set</c> subcommand. Exactly one of the
/// setting fields must be non-null.
/// </summary>
public sealed class SetCommandInputs
{
    public int? MonitorNumber { get; init; }

    public string? MonitorId { get; init; }

    public int? Brightness { get; init; }

    public int? Contrast { get; init; }

    public int? Volume { get; init; }

    public string? ColorTemperature { get; init; }

    public string? InputSource { get; init; }

    public string? PowerState { get; init; }

    public string? Orientation { get; init; }

    public bool ConfirmPowerOff { get; init; }
}
