// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>
/// Identifies an existing application that moved between visible and explicitly hidden projections.
/// </summary>
public sealed class AppVisibilityChangedEventArgs : EventArgs
{
    /// <summary>Gets the stable canonical application identity.</summary>
    public string CatalogId { get; }

    /// <summary>Gets a value indicating whether the application is now hidden.</summary>
    public bool Hidden { get; }

    /// <summary>Initializes a new instance of the <see cref="AppVisibilityChangedEventArgs"/> class. Initializes a new visibility change.</summary>
    public AppVisibilityChangedEventArgs(string catalogId, bool hidden)
    {
        CatalogId = catalogId;
        Hidden = hidden;
    }
}
