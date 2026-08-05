// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>
/// Owns the canonical application inventory and publishes stable visible and hidden projections.
/// </summary>
/// <remarks>
/// Sources, caching, reconciliation, and policy projection are implementation details of the catalog.
/// Consumers should initialize it once, take atomic snapshots, and react to deltas and refresh-state changes.
/// </remarks>
public interface IAppCatalog : IDisposable
{
    /// <summary>
    /// Raised after a practical projected-content change. No event is raised for a no-op refresh or provenance-only update.
    /// </summary>
    event EventHandler<AppCatalogChangedEventArgs>? Changed;

    /// <summary>
    /// Raised when <see cref="IsRefreshing"/> changes independently of catalog content.
    /// </summary>
    event EventHandler? RefreshStateChanged;

    /// <summary>
    /// Raised after an existing item moves between the visible and explicitly hidden projections.
    /// </summary>
    event EventHandler<AppVisibilityChangedEventArgs>? VisibilityChanged;

    /// <summary>
    /// Gets the current visible, policy-approved applications.
    /// </summary>
    IReadOnlyList<AppItem> Items { get; }

    /// <summary>
    /// Gets the current policy-approved applications that the user explicitly hid.
    /// </summary>
    IReadOnlyList<AppItem> HiddenItems { get; }

    /// <summary>
    /// Gets a value indicating whether source reconciliation is in progress.
    /// </summary>
    bool IsRefreshing { get; }

    /// <summary>
    /// Gets an atomic view of the visible and hidden projections.
    /// </summary>
    AppCatalogSnapshot GetSnapshot();

    /// <summary>
    /// Initializes the catalog once and returns when its initial cache or source result is available.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Requests a full background reconciliation of every configured source.
    /// </summary>
    Task RefreshAsync();

    /// <summary>
    /// Moves an existing application between visible and explicitly hidden projections.
    /// </summary>
    /// <param name="catalogId">The stable canonical application identity.</param>
    /// <param name="hidden"><see langword="true"/> to hide the item; otherwise, to make it visible.</param>
    Task SetAppHiddenAsync(string catalogId, bool hidden);
}
