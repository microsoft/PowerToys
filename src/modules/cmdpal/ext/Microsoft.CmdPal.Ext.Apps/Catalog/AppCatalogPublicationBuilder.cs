// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.Extensions.Logging;

using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>Builds catalog snapshots and change sets without owning refresh scheduling or synchronization.</summary>
internal sealed partial class AppCatalogPublicationBuilder
{
    private readonly IAppVisibilityStore _visibilityStore;
    private readonly IReadOnlyList<IAppCatalogFilter> _filters;
    private readonly MEL.ILogger<AppCatalog> _logger;

    internal AppCatalogPublicationBuilder(
        IAppVisibilityStore visibilityStore,
        IReadOnlyList<IAppCatalogFilter> filters,
        MEL.ILogger<AppCatalog> logger)
    {
        _visibilityStore = visibilityStore;
        _filters = filters;
        _logger = logger;
    }

    internal Publication Build(
        IReadOnlyDictionary<string, IReadOnlyList<AppCatalogItem>> snapshots,
        PublishedState previousState)
    {
        var merged = MergeSnapshots(snapshots);
        var catalogItems = new Dictionary<string, AppCatalogItem>(StringComparer.OrdinalIgnoreCase);
        var publishedApps = new Dictionary<string, AppItem>(StringComparer.OrdinalIgnoreCase);
        var visibilityById = new Dictionary<string, AppVisibility>(StringComparer.OrdinalIgnoreCase);
        List<AppItem> visibleItems = [];
        List<AppItem> hiddenItems = [];
        List<AppItem> patternHiddenItems = [];
        List<AppCatalogItemChange> changes = [];

        foreach (var item in merged.Values)
        {
            if (!IsIncluded(item))
            {
                continue;
            }

            var existed = previousState.CatalogItems.TryGetValue(item.Identity, out var previousCatalogItem);
            AppItem? app = null;
            var unchanged = existed
                && previousCatalogItem!.CanReuseMaterializedApp(item)
                && previousState.PublishedApps.TryGetValue(item.Identity, out app);

            if (!unchanged)
            {
                try
                {
                    app = item.ToAppItem();
                }
                catch (Exception ex)
                {
                    LogCachedApplicationMaterializationFailed(_logger, item.Identity, ex);
                    continue;
                }
            }

            var visibility = _visibilityStore.GetVisibility(item);
            var hidden = visibility != AppVisibility.Visible;
            catalogItems[item.Identity] = item;
            publishedApps[item.Identity] = app!;
            visibilityById[item.Identity] = visibility;
            if (visibility == AppVisibility.HiddenByPattern)
            {
                patternHiddenItems.Add(app!);
            }
            else if (visibility == AppVisibility.Hidden)
            {
                hiddenItems.Add(app!);
            }
            else
            {
                visibleItems.Add(app!);
            }

            previousState.VisibilityById.TryGetValue(item.Identity, out var previousVisibility);
            if (!existed)
            {
                changes.Add(new AppCatalogItemChange(AppCatalogChangeKind.Added, item.Identity, app, hidden));
            }
            else if (!unchanged || visibility != previousVisibility)
            {
                changes.Add(new AppCatalogItemChange(AppCatalogChangeKind.Updated, item.Identity, app, hidden));
            }
        }

        foreach (var previous in previousState.CatalogItems)
        {
            if (!catalogItems.ContainsKey(previous.Key))
            {
                changes.Add(new AppCatalogItemChange(AppCatalogChangeKind.Removed, previous.Key, null, hidden: false));
            }
        }

        return new Publication(
            new PublishedState(
                snapshots,
                new AppCatalogSnapshot(visibleItems.AsReadOnly(), hiddenItems.AsReadOnly(), patternHiddenItems.AsReadOnly()),
                catalogItems,
                publishedApps,
                visibilityById),
            changes.AsReadOnly());
    }

    private bool IsIncluded(AppCatalogItem item)
    {
        foreach (var filter in _filters)
        {
            if (!filter.Includes(item))
            {
                return false;
            }
        }

        return true;
    }

    private Dictionary<string, AppCatalogItem> MergeSnapshots(
        IReadOnlyDictionary<string, IReadOnlyList<AppCatalogItem>> snapshots)
    {
        var canonicalIdentityByTarget = BuildCanonicalIdentityByTarget(snapshots);
        var merged = new Dictionary<string, AppCatalogItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var snapshot in snapshots.Values)
        {
            foreach (var item in snapshot)
            {
                if (string.IsNullOrWhiteSpace(item.Identity))
                {
                    continue;
                }

                var canonicalItem = CanonicalizeItem(item, canonicalIdentityByTarget);
                if (!merged.TryGetValue(canonicalItem.Identity, out var existing))
                {
                    merged.Add(canonicalItem.Identity, canonicalItem);
                    continue;
                }

                try
                {
                    merged[canonicalItem.Identity] = existing.MergeProvenance(canonicalItem);
                }
                catch (InvalidOperationException ex)
                {
                    LogCatalogMergeConflict(_logger, canonicalItem.Identity, ex);
                }
            }
        }

        return merged;
    }

    private static Dictionary<string, string?> BuildCanonicalIdentityByTarget(
        IReadOnlyDictionary<string, IReadOnlyList<AppCatalogItem>> snapshots)
    {
        var canonicalIdentityByTarget = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var snapshot in snapshots.Values)
        {
            foreach (var item in snapshot)
            {
                var canonicalIdentity = item.Payload.GetCanonicalIdentityHint();
                var targetPath = item.Payload.GetCanonicalTargetPath();
                if (string.IsNullOrWhiteSpace(canonicalIdentity)
                    || string.IsNullOrWhiteSpace(targetPath))
                {
                    continue;
                }

                var target = PathHelpers.NormalizePath(targetPath);
                if (!canonicalIdentityByTarget.TryAdd(target, canonicalIdentity)
                    && !string.Equals(
                        canonicalIdentityByTarget[target],
                        canonicalIdentity,
                        StringComparison.OrdinalIgnoreCase))
                {
                    canonicalIdentityByTarget[target] = null;
                }
            }
        }

        return canonicalIdentityByTarget;
    }

    private static AppCatalogItem CanonicalizeItem(
        AppCatalogItem item,
        IReadOnlyDictionary<string, string?> canonicalIdentityByTarget)
    {
        var canonicalIdentity = item.Payload.GetCanonicalIdentityHint();
        if (!string.IsNullOrWhiteSpace(canonicalIdentity))
        {
            return item.WithIdentity(canonicalIdentity);
        }

        var targetPath = item.Payload.GetCanonicalTargetPath();
        if (string.IsNullOrWhiteSpace(targetPath)
            || !canonicalIdentityByTarget.TryGetValue(
                PathHelpers.NormalizePath(targetPath),
                out canonicalIdentity)
            || canonicalIdentity is null)
        {
            return item;
        }

        return item.WithIdentity(canonicalIdentity);
    }

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "Failed to materialize cached application '{Identity}'.")]
    private static partial void LogCachedApplicationMaterializationFailed(MEL.ILogger logger, string identity, Exception exception);

    [LoggerMessage(EventId = 10, Level = LogLevel.Warning, Message = "Ignored a conflicting representation of canonical application '{Identity}'.")]
    private static partial void LogCatalogMergeConflict(MEL.ILogger logger, string identity, Exception exception);

    internal sealed record PublishedState(
        IReadOnlyDictionary<string, IReadOnlyList<AppCatalogItem>> SourceSnapshots,
        AppCatalogSnapshot Snapshot,
        IReadOnlyDictionary<string, AppCatalogItem> CatalogItems,
        IReadOnlyDictionary<string, AppItem> PublishedApps,
        IReadOnlyDictionary<string, AppVisibility> VisibilityById)
    {
        public static PublishedState Empty { get; } = new(
            new Dictionary<string, IReadOnlyList<AppCatalogItem>>(StringComparer.Ordinal),
            new AppCatalogSnapshot([], []),
            new Dictionary<string, AppCatalogItem>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, AppItem>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, AppVisibility>(StringComparer.OrdinalIgnoreCase));
    }

    internal sealed record Publication(
        PublishedState State,
        IReadOnlyList<AppCatalogItemChange> Changes);
}
