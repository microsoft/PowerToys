// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.WinGet.Models;
using Microsoft.Management.Deployment;

namespace Microsoft.CmdPal.Common.WinGet.Services;

public interface IWinGetPackageManagerService
{
    /// <summary>
    /// Gets the current WinGet availability for this machine.
    /// </summary>
    WinGetServiceState State { get; }

    /// <summary>
    /// Searches WinGet packages using the shared package manager infrastructure.
    /// </summary>
    /// <param name="query">The search text.</param>
    /// <param name="tag">An optional package tag filter.</param>
    /// <param name="includeStoreCatalog">True to include the Store catalog in the composite search.</param>
    /// <param name="resultLimit">The maximum number of results to return.</param>
    /// <param name="cancellationToken">A token that cancels the search.</param>
    /// <returns>A query result containing matching packages or availability information.</returns>
    Task<WinGetQueryResult<IReadOnlyList<CatalogPackage>>> SearchPackagesAsync(
        string query,
        string? tag = null,
        bool includeStoreCatalog = true,
        uint resultLimit = 25,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches WinGet for Command Palette extensions and returns metadata shaped for gallery-style consumption.
    /// </summary>
    /// <param name="resultLimit">The maximum number of results to return.</param>
    /// <param name="cancellationToken">A token that cancels the search.</param>
    /// <returns>A query result containing Command Palette extension metadata.</returns>
    Task<WinGetQueryResult<IReadOnlyList<WinGetExtensionCatalogEntry>>> SearchCommandPaletteExtensionsAsync(
        uint resultLimit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves packages by WinGet package id.
    /// </summary>
    /// <param name="packageIds">The package ids to resolve.</param>
    /// <param name="includeStoreCatalog">True to include the Store catalog in the lookup.</param>
    /// <param name="cancellationToken">A token that cancels the lookup.</param>
    /// <returns>A query result containing the resolved packages keyed by package id.</returns>
    Task<WinGetQueryResult<IReadOnlyDictionary<string, CatalogPackage>>> GetPackagesByIdAsync(
        IEnumerable<string> packageIds,
        bool includeStoreCatalog = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs or updates the provided package and refreshes package catalogs when possible.
    /// </summary>
    /// <param name="package">The package to install or update.</param>
    /// <param name="skipDependencies">True to skip dependent packages when supported.</param>
    /// <param name="progressHandler">An optional callback that receives install progress updates.</param>
    /// <param name="cancellationToken">A token that cancels the install or update.</param>
    /// <returns>The final result of the install or update operation.</returns>
    Task<WinGetPackageOperationResult> InstallPackageAsync(
        CatalogPackage package,
        bool skipDependencies = false,
        Action<InstallProgress>? progressHandler = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uninstalls the provided package and refreshes package catalogs when possible.
    /// </summary>
    /// <param name="package">The package to uninstall.</param>
    /// <param name="progressHandler">An optional callback that receives uninstall progress updates.</param>
    /// <param name="cancellationToken">A token that cancels the uninstall.</param>
    /// <returns>The final result of the uninstall operation.</returns>
    Task<WinGetPackageOperationResult> UninstallPackageAsync(
        CatalogPackage package,
        Action<UninstallProgress>? progressHandler = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes WinGet package catalogs when supported and clears cached composite catalogs.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the refresh.</param>
    /// <returns>True when catalog refresh was attempted successfully; otherwise, false.</returns>
    Task<bool> RefreshCatalogsAsync(CancellationToken cancellationToken = default);
}
