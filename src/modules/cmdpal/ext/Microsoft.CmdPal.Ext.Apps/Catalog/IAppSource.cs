// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>
/// Discovers application candidates from one independently refreshable and cacheable origin.
/// </summary>
/// <remarks>
/// Implementations report invalidation but never publish directly to pages. The catalog serializes all source work.
/// </remarks>
internal interface IAppSource : IDisposable
{
    /// <summary>
    /// Raised when the source can no longer guarantee that its last snapshot is current.
    /// </summary>
    event EventHandler<AppSourceInvalidatedEventArgs>? Invalidated;

    /// <summary>
    /// Gets the stable source identity used for cache and refresh scoping.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets inexpensive state used to decide whether this source's cached snapshot is reusable.
    /// </summary>
    string CacheKey { get; }

    /// <summary>
    /// Initializes source-owned operating-system resources without blocking the caller's thread.
    /// </summary>
    /// <remarks>
    /// Discovery remains in <see cref="LoadAsync"/>. This hook is for resources such as file-system watchers.
    /// </remarks>
    Task InitializeAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Produces a complete authoritative snapshot for this source.
    /// </summary>
    /// <remarks>
    /// Return <see cref="AppSourceScanResult"/> when reads were incomplete or candidate paths need a delayed retry.
    /// Full scans set <see cref="AppSourceScanResult.IsFullScan"/>, including when reached through dirty-path reconciliation.
    /// Background recovery may reuse unchanged candidates and must lower the priority of synchronous discovery work.
    /// </remarks>
    Task<IReadOnlyList<AppCatalogItem>> LoadAsync(CancellationToken cancellationToken, bool background = false);

    /// <summary>
    /// Reconciles dirty paths against the last committed source snapshot.
    /// </summary>
    /// <remarks>
    /// Notifications are not ordered state transitions. Implementations must inspect current source state and make
    /// repeated reconciliation idempotent. The default implementation performs a full load, so a source remains
    /// correct without incremental support.
    /// A promoted full scan reports <see cref="AppSourceScanResult.IsFullScan"/> so retry and cache bookkeeping use its actual scope.
    /// </remarks>
    Task<IReadOnlyList<AppCatalogItem>> ApplyChangesAsync(
        IReadOnlyList<AppCatalogItem> currentItems,
        IReadOnlyList<AppSourcePathChange> changes,
        CancellationToken cancellationToken,
        bool background = false)
    {
        return LoadAsync(cancellationToken, background);
    }
}
