// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.CmdPal.Common.WinGet.Models;
using Microsoft.Management.Deployment;

namespace Microsoft.CmdPal.Common.WinGet.Services;

public sealed class WinGetPackageStatusService : IWinGetPackageStatusService
{
    private static readonly StringComparer OrdinalIgnoreCase = StringComparer.OrdinalIgnoreCase;
    private readonly IWinGetPackageManagerService _packageManagerService;

    public WinGetPackageStatusService(IWinGetPackageManagerService packageManagerService)
    {
        _packageManagerService = packageManagerService;
    }

    public async Task<IReadOnlyDictionary<string, WinGetPackageInfo>?> TryGetPackageInfosAsync(
        IEnumerable<string> packageIds,
        CancellationToken cancellationToken = default)
    {
        var normalizedIds = NormalizePackageIds(packageIds);
        if (normalizedIds.Count == 0)
        {
            return new Dictionary<string, WinGetPackageInfo>(OrdinalIgnoreCase);
        }

        return await TryGetInfosViaWinGetApiAsync(normalizedIds, _packageManagerService, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, WinGetPackageStatus>?> TryGetPackageStatusesAsync(
        IEnumerable<string> packageIds,
        CancellationToken cancellationToken = default)
    {
        var infos = await TryGetPackageInfosAsync(packageIds, cancellationToken);
        if (infos is null)
        {
            return null;
        }

        Dictionary<string, WinGetPackageStatus> statuses = new(OrdinalIgnoreCase);
        foreach (var pair in infos)
        {
            statuses[pair.Key] = pair.Value.Status;
        }

        return statuses;
    }

    private static async Task<IReadOnlyDictionary<string, WinGetPackageInfo>?> TryGetInfosViaWinGetApiAsync(
        IReadOnlyList<string> packageIds,
        IWinGetPackageManagerService packageManagerService,
        CancellationToken cancellationToken)
    {
        var packagesResult = await packageManagerService.GetPackagesByIdAsync(packageIds, includeStoreCatalog: false, cancellationToken);
        if (!packagesResult.IsSuccess || packagesResult.Value is null)
        {
            return null;
        }

        try
        {
            Dictionary<string, WinGetPackageInfo> results = new(OrdinalIgnoreCase);
            for (var i = 0; i < packageIds.Count; i++)
            {
                var packageId = packageIds[i];
                var status = new WinGetPackageStatus(
                    IsInstalled: false,
                    IsInstalledStateKnown: true,
                    IsUpdateAvailable: false,
                    IsUpdateStateKnown: true);
                results[packageId] = new WinGetPackageInfo(status, Details: null);
            }

            foreach (var package in packagesResult.Value.Values)
            {
                if (!results.ContainsKey(package.Id))
                {
                    continue;
                }

                results[package.Id] = await InspectPackageAsync(package);
            }

            return results;
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException or TaskCanceledException)
        {
            CoreLogger.LogWarning($"WinGet API package info query failed: {ex.Message}");
            return null;
        }
    }

    private static async Task<WinGetPackageInfo> InspectPackageAsync(CatalogPackage package)
    {
        var status = await WinGetPackageMetadataHelper.InspectPackageStatusAsync(package);
        var details = WinGetPackageMetadataHelper.TryBuildPackageDetails(package);
        return new WinGetPackageInfo(status, details);
    }

    private static List<string> NormalizePackageIds(IEnumerable<string> packageIds)
    {
        List<string> normalized = [];
        HashSet<string> seen = new(OrdinalIgnoreCase);

        foreach (var candidate in packageIds)
        {
            var trimmed = ToNullIfWhiteSpace(candidate);
            if (trimmed is null || !seen.Add(trimmed))
            {
                continue;
            }

            normalized.Add(trimmed);
        }

        return normalized;
    }

    private static string? ToNullIfWhiteSpace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
