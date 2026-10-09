// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace PowerDisplay.Contracts;

/// <summary>
/// Result envelope of <c>powerdisplay get</c>. Always carries a list — a single-monitor
/// query produces a one-element list; a no-selector query produces one entry per
/// discovered monitor. Consumers always iterate <see cref="Monitors"/>.
/// </summary>
public sealed class CliGetResult
{
    // Response discriminator (see CliResponseHeader): false on success DTOs, true only on CliErrorResult.
    public bool IsError { get; init; }

    public string Version { get; init; } = CliSchema.Version;

    public string Command { get; init; } = CliCommandNames.Get;

    public IReadOnlyList<CliGetMonitorEntry> Monitors { get; init; } = [];
}
