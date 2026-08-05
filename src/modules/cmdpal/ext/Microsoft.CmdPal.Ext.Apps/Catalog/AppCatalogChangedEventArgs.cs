// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>
/// Provides the practical item changes between two atomically published catalog projections.
/// </summary>
public sealed class AppCatalogChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the added, updated, and removed items. The collection is never empty.
    /// </summary>
    public IReadOnlyList<AppCatalogItemChange> Changes { get; }

    /// <summary>Initializes a new instance of the <see cref="AppCatalogChangedEventArgs"/> class. Captures the item changes associated with one published catalog update.</summary>
    internal AppCatalogChangedEventArgs(IReadOnlyList<AppCatalogItemChange> changes)
    {
        Changes = changes;
    }
}
