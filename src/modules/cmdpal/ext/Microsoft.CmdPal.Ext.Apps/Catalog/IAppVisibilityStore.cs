// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>
/// Reads and mutates the user's explicit visibility preference for canonical catalog items.
/// </summary>
/// <remarks>
/// Visibility is separate from automatic catalog exclusion so hidden items can be shown and restored by the UI.
/// </remarks>
internal interface IAppVisibilityStore
{
    /// <summary>
    /// Determines whether the user explicitly hid the item.
    /// </summary>
    bool IsHidden(AppCatalogItem item);

    /// <summary>
    /// Updates the in-memory preference and reports whether it changed.
    /// </summary>
    bool SetHidden(AppCatalogItem item, bool hidden);

    /// <summary>
    /// Persists pending in-memory visibility changes.
    /// </summary>
    void Persist();
}
