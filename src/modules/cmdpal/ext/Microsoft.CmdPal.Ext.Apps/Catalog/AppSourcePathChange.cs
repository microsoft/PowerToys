// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>
/// Identifies a path whose current source state must be reconciled.
/// </summary>
/// <remarks>
/// File-system notifications are asynchronous hints. <see cref="ObservedKind"/> is retained for
/// diagnostics, but sources must probe <see cref="Path"/> instead of treating it as a state transition.
/// </remarks>
internal sealed class AppSourcePathChange
{
    /// <summary>Gets the observed file-system notification kind for diagnostics.</summary>
    public WatcherChangeTypes ObservedKind { get; }

    /// <summary>Gets the path whose current state must be reconciled.</summary>
    public string Path { get; }

    /// <summary>Gets an additional dirty path reported as the old side of a rename.</summary>
    public string? OldPath { get; }

    /// <summary>Initializes a new instance of the <see cref="AppSourcePathChange"/> class. Initializes a new dirty-path notification.</summary>
    public AppSourcePathChange(WatcherChangeTypes observedKind, string path, string? oldPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ObservedKind = observedKind;
        Path = path;
        OldPath = oldPath;
    }
}
