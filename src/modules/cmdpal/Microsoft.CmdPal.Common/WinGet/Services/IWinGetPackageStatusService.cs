// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.WinGet.Models;

namespace Microsoft.CmdPal.Common.WinGet.Services;

public interface IWinGetPackageStatusService
{
    /// <summary>
    /// Tries to resolve WinGet package information for the provided package ids.
    /// Returns null when WinGet lookups are unavailable.
    /// </summary>
    /// <param name="packageIds">The WinGet package ids to resolve.</param>
    /// <param name="cancellationToken">A token that cancels the lookup.</param>
    /// <returns>A package-info map keyed by package id, or null when status lookups are unavailable.</returns>
    Task<IReadOnlyDictionary<string, WinGetPackageInfo>?> TryGetPackageInfosAsync(
        IEnumerable<string> packageIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to resolve WinGet install/update status for the provided package ids.
    /// Returns null when status detection is unavailable.
    /// </summary>
    /// <param name="packageIds">The WinGet package ids to inspect.</param>
    /// <param name="cancellationToken">A token that cancels the lookup.</param>
    /// <returns>A package-status map keyed by package id, or null when status detection is unavailable.</returns>
    Task<IReadOnlyDictionary<string, WinGetPackageStatus>?> TryGetPackageStatusesAsync(
        IEnumerable<string> packageIds,
        CancellationToken cancellationToken = default);
}
