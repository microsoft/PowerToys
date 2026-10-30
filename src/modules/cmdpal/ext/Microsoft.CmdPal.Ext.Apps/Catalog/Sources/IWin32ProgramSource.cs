// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

/// <summary>
/// Describes one independently refreshable Win32 discovery origin and its path interpretation policy.
/// </summary>
internal interface IWin32ProgramSource
{
    /// <summary>Gets the stable identity used for provenance and cache validation.</summary>
    string Id { get; }

    /// <summary>Gets the representation preference; lower values are preferred.</summary>
    int Priority { get; }

    /// <summary>Gets a value indicating whether this origin participates in discovery.</summary>
    bool IsEnabled { get; }

    /// <summary>Gets the scan and interpretation policy for paths contributed by this origin.</summary>
    Win32ProgramSourceProfile Profile { get; }

    /// <summary>Gets whether this origin can enumerate a dirty path without a full source scan.</summary>
    bool SupportsIncrementalChanges => false;

    /// <summary>
    /// Gets the greatest directory depth scanned below each watch root. Zero scans only the root;
    /// <see cref="int.MaxValue"/> scans every descendant allowed by the source profile.
    /// </summary>
    int MaximumDepth => (Profile & Win32ProgramSourceProfile.RecurseSubdirectories) != 0 ? int.MaxValue : 0;

    /// <summary>Gets inexpensive state used to validate this origin's cached contribution.</summary>
    string CacheKey { get; }

    /// <summary>Gets settings-derived state used to decide whether an existing source instance can be retained.</summary>
    string ConfigurationKey { get; }

    /// <summary>Gets the directories whose changes can invalidate this origin.</summary>
    IReadOnlyList<string> WatchPaths { get; }

    /// <summary>Enumerates candidate paths from this origin.</summary>
    IEnumerable<string> GetPaths();

    /// <summary>Enumerates candidate paths together with their search metadata.</summary>
    IEnumerable<Win32ProgramCandidate> GetCandidates(Action<string, Exception>? onError = null, CancellationToken cancellationToken = default)
    {
        foreach (var path in GetPaths())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new Win32ProgramCandidate(path, []);
        }
    }

    /// <summary>
    /// Enumerates candidates that currently exist at or below a dirty path, constrained by this
    /// origin's configured roots, suffixes, and maximum depth.
    /// </summary>
    /// <remarks>
    /// The observed watcher event is intentionally not supplied. Implementations must probe current
    /// state so duplicate, reordered, or superseded notifications converge on the same result.
    /// Confirmed missing paths are reported separately so unreadable parents cannot preserve them.
    /// </remarks>
    IEnumerable<string> GetPathsForChange(
        string path,
        Action<string, Exception>? onError = null,
        Action<string>? onMissing = null,
        CancellationToken cancellationToken = default)
    {
        return [];
    }

    /// <summary>Determines whether a watcher path can affect this origin's candidate set.</summary>
    bool IsRelevantPath(string path);
}
