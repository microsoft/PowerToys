// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Cli.Commands;

/// <summary>
/// Inputs collected from the parsed <c>up</c>/<c>down</c> subcommand. Exactly one of the three
/// continuous-setting flags must be true. <see cref="Step"/> is null when <c>--step</c> is omitted.
/// </summary>
public sealed class AdjustCommandInputs
{
    public int? MonitorNumber { get; init; }

    public string? MonitorId { get; init; }

    public bool Brightness { get; init; }

    public bool Contrast { get; init; }

    public bool Volume { get; init; }

    public int? Step { get; init; }
}
