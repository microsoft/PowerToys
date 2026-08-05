// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>
/// Defines a cheap, synchronous inclusion policy over canonical catalog items.
/// </summary>
/// <remarks>
/// Filters must not perform I/O. A change re-projects the current inventory without refreshing sources or rewriting cache.
/// </remarks>
internal interface IAppCatalogFilter
{
    /// <summary>
    /// Raised when the policy result may have changed for existing items.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>
    /// Determines whether an item is eligible for the visible or explicitly hidden projections.
    /// </summary>
    bool Includes(AppCatalogItem item);
}
