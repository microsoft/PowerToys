// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>
/// Reads visibility rules and mutates explicit visibility preferences for canonical catalog items.
/// </summary>
/// <remarks>
/// Hidden items remain available in the Hidden apps view; catalog filters can exclude items entirely.
/// </remarks>
internal interface IAppVisibilityStore : IDisposable
{
    event EventHandler? Changed;

    /// <summary>
    /// Determines whether the item is visible, explicitly hidden, or hidden by a pattern.
    /// </summary>
    AppVisibility GetVisibility(AppCatalogItem item);

    /// <summary>
    /// Updates the in-memory preference and reports whether it changed.
    /// </summary>
    bool SetHidden(AppCatalogItem item, bool hidden);

    /// <summary>
    /// Persists pending in-memory visibility changes.
    /// </summary>
    void Persist();
}
