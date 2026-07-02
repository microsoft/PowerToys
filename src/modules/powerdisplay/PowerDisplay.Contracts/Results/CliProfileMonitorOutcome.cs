// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace PowerDisplay.Contracts;

/// <summary>
/// Per-monitor outcome of an <c>apply-profile</c> run.
/// </summary>
public sealed class CliProfileMonitorOutcome
{
    public CliMonitorRef Monitor { get; init; } = new();

    /// <summary>
    /// False when the profile names a monitor that is not currently connected (or is hidden);
    /// in that case <see cref="Changes"/> is empty and nothing was written.
    /// </summary>
    public bool Connected { get; init; }

    public IReadOnlyList<CliProfileChange> Changes { get; init; } = [];
}
