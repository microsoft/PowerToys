// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.CmdPal.Ext.Apps.Utils;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>Reports discovered items together with incomplete reads and candidates worth retrying.</summary>
internal sealed class AppSourceScanResult : ReadOnlyCollection<AppCatalogItem>
{
    public bool IsComplete { get; }

    /// <summary>Gets whether the scan covered the whole source, including when reads were incomplete.</summary>
    public bool IsFullScan { get; }

    public IReadOnlyList<string> RetryPaths { get; }

    /// <summary>Gets unreadable files or subtrees, or null when failure coverage is unknown.</summary>
    public IReadOnlyList<string>? FailedPaths { get; }

    /// <summary>Gets source paths whose current state was successfully established.</summary>
    public IReadOnlySet<string> CheckedPaths { get; }

    /// <summary>Gets package families whose applications could not be read.</summary>
    public IReadOnlyList<string> FailedPackageFamilies { get; }

    public AppSourceScanResult(
        IList<AppCatalogItem> items,
        bool isComplete = true,
        IReadOnlyList<string>? retryPaths = null,
        IReadOnlyList<string>? failedPaths = null,
        IReadOnlySet<string>? checkedPaths = null,
        IReadOnlyList<string>? failedPackageFamilies = null,
        bool isFullScan = false)
        : base(items)
    {
        IsComplete = isComplete;
        IsFullScan = isFullScan;
        RetryPaths = retryPaths ?? [];
        FailedPaths = failedPaths;
        CheckedPaths = checkedPaths ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        FailedPackageFamilies = failedPackageFamilies ?? [];
    }

    /// <summary>Preserves only representations whose current source state is still unknown.</summary>
    public AppCatalogItem? GetRetainedItem(AppCatalogItem item)
    {
        if (IsComplete)
        {
            return null;
        }

        if (FailedPaths is null)
        {
            return item;
        }

        var references = item.Provenance.References.Where(IsUnreadableReference).ToImmutableArray();
        if (references.Length > 0)
        {
            if (references.Length == item.Provenance.References.Length)
            {
                return item;
            }

            var payload = item.Payload;
            if (payload is Win32AppPayload win32 && !references.Any(reference => StringComparer.OrdinalIgnoreCase.Equals(reference.ItemId, win32.LnkFilePath)))
            {
                // The formerly preferred shortcut may now launch a different app; use its last known target.
                payload = win32 with { LnkFilePath = string.Empty };
            }

            var removedPrefixes = item.Provenance.References.Except(references)
                .Select(reference => $"win32:{LaunchTarget.FilePath(reference.ItemId).IdentityToken}|args:")
                .ToArray();
            return new AppCatalogItem(
                item.Identity,
                new AppCatalogProvenance(item.Provenance.Priority, references[0], references),
                item.MatchTerms,
                payload,
                item.CommandIds.Where(id => !this.Any(current => !StringComparer.OrdinalIgnoreCase.Equals(current.Identity, item.Identity)
                    && current.CommandIds.Contains(id, StringComparer.Ordinal))).ToImmutableArray(),
                item.IdentityAliases.Where(alias => !removedPrefixes.Any(prefix => alias.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))).ToImmutableArray());
        }

        if (item.Payload is PackagedAppSnapshot packaged && !CheckedPaths.Contains(packaged.UserModelId))
        {
            foreach (var family in FailedPackageFamilies)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(packaged.PackageFamilyName, family))
                {
                    return item;
                }
            }
        }

        return null;
    }

    private bool IsUnreadableReference(AppCatalogSourceReference reference)
    {
        if (CheckedPaths.Contains(reference.ItemId))
        {
            return false;
        }

        foreach (var path in FailedPaths!)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(reference.ItemId, path)
                || PathHelpers.IsPathInsideDirectory(reference.ItemId, path))
            {
                return true;
            }
        }

        return false;
    }
}
