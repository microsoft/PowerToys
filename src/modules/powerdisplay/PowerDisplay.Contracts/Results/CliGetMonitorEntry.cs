// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace PowerDisplay.Contracts;

/// <summary>
/// One monitor's current-settings block inside a <see cref="CliGetResult"/>. Carries
/// the monitor metadata (number, id, name, transport) alongside its setting values
/// so a single-monitor and an all-monitors get share the same per-entry shape.
/// </summary>
public sealed class CliGetMonitorEntry
{
    public CliMonitorRef Monitor { get; init; } = new();

    public IReadOnlyList<CliSettingValue> Settings { get; init; } = [];
}
