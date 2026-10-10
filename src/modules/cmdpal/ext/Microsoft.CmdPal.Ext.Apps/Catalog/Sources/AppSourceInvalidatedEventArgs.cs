// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

/// <summary>
/// Describes the most precise refresh a source can request after its committed snapshot may have changed.
/// </summary>
internal sealed class AppSourceInvalidatedEventArgs : EventArgs
{
    /// <summary>Gets a value indicating whether the source requires full reconciliation.</summary>
    public bool RequiresFullRefresh => PathChange is null;

    /// <summary>Gets the dirty-path hint, or <see langword="null"/> for full reconciliation.</summary>
    public AppSourcePathChange? PathChange { get; }

    /// <summary>Gets a reusable full-reconciliation request.</summary>
    public static AppSourceInvalidatedEventArgs FullRefresh { get; } = new(null);

    private AppSourceInvalidatedEventArgs(AppSourcePathChange? pathChange)
    {
        PathChange = pathChange;
    }

    /// <summary>Creates an incremental invalidation for one dirty path.</summary>
    public static AppSourceInvalidatedEventArgs ForPath(AppSourcePathChange pathChange)
    {
        ArgumentNullException.ThrowIfNull(pathChange);

        return new(pathChange);
    }
}
