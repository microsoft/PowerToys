// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerToys.WorkspacesMCP.Models;

namespace PowerToys.WorkspacesMCP.Services;

/// <summary>
/// Immutable point-in-time snapshot of the workspace (applications + windows).
/// </summary>
public sealed record ImmutableWorkspaceSnapshot(
    DateTime TimestampUtc,
    IReadOnlyList<AppInfo> Apps,
    IReadOnlyList<WindowInfo> Windows,
    int VisibleWindows,
    long Version);
