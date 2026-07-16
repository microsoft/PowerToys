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
    /// <param name="loadProfilesAsync">Lazy profile loader, invoked only by profile commands.</param>
    /// <param name="applyProfileAsync">
    /// Applies a profile by id and returns its resolved name; <see langword="null"/> result means
    /// "not found".
    /// </param>
    public CliCommandContext(
        CliRequestEnvelope envelope,
        IReadOnlyList<Monitor> snapshot,
        IReadOnlySet<string> hiddenIds,
        IReadOnlyList<CustomVcpValueMapping> customMappings,
        IMonitorManager manager,
        int defaultStep,
        Func<CancellationToken, Task<PowerDisplayProfiles>> loadProfilesAsync,
        Func<int, CancellationToken, Task<string?>> applyProfileAsync)
    {
        Envelope = envelope;
        Snapshot = snapshot;
        HiddenIds = hiddenIds;
        CustomMappings = customMappings;
        Manager = manager;
        DefaultStep = defaultStep;
        LoadProfilesAsync = loadProfilesAsync;
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

    /// <summary>Lazy asynchronous profile loader, invoked only by profile commands.</summary>
    public Func<CancellationToken, Task<PowerDisplayProfiles>> LoadProfilesAsync { get; }

    /// <summary>
    /// Applies a profile by id (best-effort) and returns the resolved profile's name;
    /// <see langword="null"/> when the profile is not found. The apply-profile handler uses the
    /// returned name directly and must not call <see cref="LoadProfilesAsync"/> to recover it.
    /// </summary>
    public Func<int, CancellationToken, Task<string?>> ApplyProfileAsync { get; }
}
