// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>
/// Describes one canonical application's change in the visible or hidden projection.
/// </summary>
public sealed class AppCatalogItemChange
{
    /// <summary>Gets the kind of change.</summary>
    public AppCatalogChangeKind Kind { get; }

    /// <summary>Gets the stable canonical identity affected by the change.</summary>
    public string CatalogId { get; }

    /// <summary>
    /// Gets the current application payload for added and updated changes, or <see langword="null"/> for removals.
    /// </summary>
    public AppItem? Item { get; }

    /// <summary>
    /// Gets a value indicating whether the current payload belongs to the explicitly hidden projection.
    /// </summary>
    public bool Hidden { get; }

    /// <summary>Initializes a new instance of the <see cref="AppCatalogItemChange"/> class. Describes an added, updated, or removed app and its resulting visibility.</summary>
    /// <param name="kind">The change relative to the previous publication.</param>
    /// <param name="catalogId">The canonical identity of the affected app.</param>
    /// <param name="item">The current app, or null for a removal.</param>
    /// <param name="hidden">Whether the current app is hidden from ordinary discovery.</param>
    internal AppCatalogItemChange(AppCatalogChangeKind kind, string catalogId, AppItem? item, bool hidden)
    {
        Kind = kind;
        CatalogId = catalogId;
        Item = item;
        Hidden = hidden;
    }
}
