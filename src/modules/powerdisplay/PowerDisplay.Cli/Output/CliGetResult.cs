// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace PowerDisplay.Cli.Output;

/// <summary>
/// Result envelope of <c>powerdisplay get</c>. Always carries a list — a single-monitor
/// query produces a one-element list; a no-selector query produces one entry per
/// discovered monitor. Consumers always iterate <see cref="Monitors"/>.
/// </summary>
public sealed class CliGetResult
{
    public bool Ok { get; init; } = true;

    public string Command { get; init; } = "get";

    public IReadOnlyList<CliGetMonitorEntry> Monitors { get; init; } = [];
}
