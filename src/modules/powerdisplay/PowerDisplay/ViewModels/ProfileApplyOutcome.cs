// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using PowerDisplay.Contracts;

namespace PowerDisplay.ViewModels;

/// <summary>
/// Per-monitor outcome of applying a profile. Used by IPC callers to build
/// <see cref="PowerDisplay.Contracts.CliApplyProfileResult"/> without re-running hardware operations.
/// </summary>
/// <param name="MonitorId">The monitor's unique identifier from the profile entry.</param>
/// <param name="Connected">
/// <c>true</c> when a live <see cref="MonitorViewModel"/> was found for this monitor;
/// <c>false</c> when the profile names a monitor that is not currently connected (no hardware
/// writes were attempted).
/// </param>
/// <param name="Changes">
/// Per-setting outcomes. Each element is a <see cref="CliProfileChange"/> carrying the
/// canonical setting name, the raw value requested, an optional human-readable display string
/// (present only on success), the status string, and an optional error message (present only
/// on hardware failure). Empty when <see cref="Connected"/> is <c>false</c>.
/// </param>
/// <param name="Number">
/// 1-based monitor number for the connected monitor, mirrored into the result's
/// <see cref="PowerDisplay.Contracts.CliMonitorRef.Number"/> so the CLI renderer prints the real
/// label ("Monitor 2 (Dell ...)"). Defaults to 0 for disconnected entries (the renderer falls back
/// to the monitor Id for those).
/// </param>
/// <param name="Name">
/// Friendly monitor name for the connected monitor, mirrored into
/// <see cref="PowerDisplay.Contracts.CliMonitorRef.Name"/>. Defaults to empty for disconnected entries.
/// </param>
public readonly record struct ProfileApplyOutcome(
    string MonitorId,
    bool Connected,
    IReadOnlyList<CliProfileChange> Changes,
    int Number = 0,
    string Name = "");
