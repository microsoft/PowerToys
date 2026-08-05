// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

/// <summary>
/// Describes one Win32 discovery origin that contributes paths and source policy to the combined Win32 catalog source.
/// </summary>
internal interface IWin32ProgramSource
{
    /// <summary>Gets the stable identity used for provenance and cache validation.</summary>
    string Id { get; }

    /// <summary>Gets the representation preference; lower values are preferred.</summary>
    int Priority { get; }

    /// <summary>Gets a value indicating whether this origin participates in discovery.</summary>
    bool IsEnabled { get; }

    /// <summary>Gets a value indicating whether generic files and folders from this origin are eligible.</summary>
    bool IncludeNonApps { get; }

    /// <summary>Gets a value indicating whether discovered executables are represented as direct run commands.</summary>
    bool AsRunCommand { get; }

    /// <summary>Gets inexpensive state used to validate this origin's cached contribution.</summary>
    string CacheKey { get; }

    /// <summary>Gets settings-derived state used to decide whether an existing source instance can be retained.</summary>
    string ConfigurationKey { get; }

    /// <summary>Gets the directories whose changes can invalidate this origin.</summary>
    IReadOnlyList<string> WatchPaths { get; }

    /// <summary>Enumerates candidate paths from this origin.</summary>
    IEnumerable<string> GetPaths();

    /// <summary>Determines whether a watcher path can affect this origin's candidate set.</summary>
    bool IsRelevantPath(string path);
}
