// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>
/// Persists raw per-source catalog snapshots independently of user-facing projections.
/// </summary>
internal interface IAppCatalogCache
{
    /// <summary>
    /// Loads the source snapshots that remain valid in the supplied environment.
    /// </summary>
    /// <returns>Validated source snapshots, or null when no usable cache is available.</returns>
    Task<AppCatalogCacheFile?> LoadAsync(AppCatalogCacheContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically persists committed source snapshots, renewing validation only after a complete source scan.
    /// </summary>
    /// <remarks>
    /// Partial scans retain earlier validation and cannot establish a new source key. Unchanged writes may be coalesced.
    /// </remarks>
    Task SaveAsync(
        IReadOnlyDictionary<string, IReadOnlyList<AppCatalogItem>> sourceSnapshots,
        IReadOnlyCollection<string> fullyReconciledSourceIds,
        AppCatalogCacheContext context,
        CancellationToken cancellationToken);
}
