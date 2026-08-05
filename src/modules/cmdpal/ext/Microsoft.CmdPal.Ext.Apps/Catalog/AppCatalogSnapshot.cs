// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>
/// Captures the visible and explicitly hidden projections from one atomic catalog publication.
/// </summary>
public sealed class AppCatalogSnapshot
{
    internal AppCatalogSnapshot(IReadOnlyList<AppItem> items, IReadOnlyList<AppItem> hiddenItems)
    {
        Items = items;
        HiddenItems = hiddenItems;
    }

    /// <summary>Gets visible, policy-approved applications.</summary>
    public IReadOnlyList<AppItem> Items { get; }

    /// <summary>Gets policy-approved applications that the user explicitly hid.</summary>
    public IReadOnlyList<AppItem> HiddenItems { get; }
}
