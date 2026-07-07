// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Common.Services;
using PowerDisplay.Contracts;
using PowerDisplay.Models;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Ipc;

/// <summary>
/// Immutable per-request snapshot of the ViewModel/MonitorManager state a single CLI command
/// operates on, plus the lazy profile delegates. Built once on the UI thread and handed to the
/// matching <see cref="ICliCommandHandler"/>, so handlers never touch the live ViewModel directly.
/// </summary>
internal sealed class CliCommandContext
{
    /// <summary>Initializes the context with the pre-fetched request state and profile delegates.</summary>
    /// <param name="envelope">The parsed request envelope (command + typed payload).</param>
    /// <param name="snapshot">Pre-fetched monitor list from <c>MainViewModel.SnapshotMonitors()</c>.</param>
    /// <param name="hiddenIds">Pre-fetched hidden-ID set from <c>MainViewModel.GetHiddenMonitorIds()</c>.</param>
    /// <param name="customMappings">User-defined VCP value name mappings.</param>
    /// <param name="manager">The live <see cref="IMonitorManager"/> for hardware writes.</param>
    /// <param name="defaultStep">The default relative-adjust step (mouse-wheel increment).</param>
    /// <param name="loadProfiles">Lazy profile loader, invoked only by the <c>profiles</c> command.</param>
    /// <param name="applyProfileAsync">Applies a profile by id; <c>false</c> result means "not found".</param>
    public CliCommandContext(
        CliRequestEnvelope envelope,
        IReadOnlyList<Monitor> snapshot,
        IReadOnlySet<string> hiddenIds,
        IReadOnlyList<CustomVcpValueMapping> customMappings,
        IMonitorManager manager,
        int defaultStep,
        Func<PowerDisplayProfiles> loadProfiles,
        Func<int, CancellationToken, Task<bool>> applyProfileAsync)
    {
        Envelope = envelope;
        Snapshot = snapshot;
        HiddenIds = hiddenIds;
        CustomMappings = customMappings;
        Manager = manager;
        DefaultStep = defaultStep;
        LoadProfiles = loadProfiles;
        ApplyProfileAsync = applyProfileAsync;
    }

    /// <summary>The parsed request envelope (command + typed payload).</summary>
    public CliRequestEnvelope Envelope { get; }

    /// <summary>Pre-fetched monitor list snapshot.</summary>
    public IReadOnlyList<Monitor> Snapshot { get; }

    /// <summary>Set of monitor IDs hidden by user preference.</summary>
    public IReadOnlySet<string> HiddenIds { get; }

    /// <summary>User-defined VCP value name mappings.</summary>
    public IReadOnlyList<CustomVcpValueMapping> CustomMappings { get; }

    /// <summary>The live monitor manager for hardware writes.</summary>
    public IMonitorManager Manager { get; }

    /// <summary>The default relative-adjust step (mouse-wheel increment).</summary>
    public int DefaultStep { get; }

    /// <summary>Lazy profile loader, invoked only by the <c>profiles</c> command.</summary>
    public Func<PowerDisplayProfiles> LoadProfiles { get; }

    /// <summary>Applies a profile by id (best-effort); returns <c>false</c> when the profile is not found.</summary>
    public Func<int, CancellationToken, Task<bool>> ApplyProfileAsync { get; }
}
