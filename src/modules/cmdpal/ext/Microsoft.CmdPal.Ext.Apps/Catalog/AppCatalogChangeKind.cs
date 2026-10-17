// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>
/// Describes how a canonical item changed in a projected catalog snapshot.
/// </summary>
public enum AppCatalogChangeKind
{
    /// <summary>An item entered the projection.</summary>
    Added,

    /// <summary>An existing projected item's application content or visibility changed.</summary>
    Updated,

    /// <summary>An item left the projection.</summary>
    Removed,
}
