// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.Extensions.Logging;

using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>Builds catalog snapshots and change sets without scheduling refreshes or persisting data.</summary>
internal sealed partial class AppCatalogPublicationBuilder
{
    private readonly IReadOnlyList<IAppCatalogFilter> _filters;
    private readonly MEL.ILogger<AppCatalog> _logger;

    /// <summary>Initializes a new instance of the <see cref="AppCatalogPublicationBuilder"/> class. Creates a publication builder using the catalog's filters and diagnostic logger.</summary>
    internal AppCatalogPublicationBuilder(
        IReadOnlyList<IAppCatalogFilter> filters,
        MEL.ILogger<AppCatalog> logger)
    {
        _filters = filters;
        _logger = logger;
    }

    /// <summary>Builds merged app projections, retained aliases, and deltas without committing or persisting them.</summary>
    /// <remarks>Unchanged app objects are reused; aliases and visibility are calculated from the same merged inventory.</remarks>
    internal Publication Build(
        IReadOnlyDictionary<string, IReadOnlyList<AppCatalogItem>> snapshots,
        PublishedState previousState,
        FrozenDictionary<string, string> savedAliases,
        IReadOnlySet<string> hiddenIdentities,
        AppCatalogVisibility visibilityRules)
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

            catalogItems[item.Identity] = item;
            publishedApps[item.Identity] = app!;
        }

        // Retain redirects before classifying visibility so moved apps are hidden in their first publication.
        var commandAliases = AppCatalogCommandAliases.Retain(savedAliases, publishedApps.Values);
        var hiddenCommandIds = AppCatalogVisibility.ResolveHiddenCommandIds(hiddenIdentities, commandAliases);
        foreach (var item in catalogItems.Values)
        {
            var app = publishedApps[item.Identity];
            var visibility = visibilityRules.GetVisibility(item, hiddenIdentities, hiddenCommandIds);
            var hidden = visibility != AppVisibility.Visible;
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

            var existed = previousState.CatalogItems.ContainsKey(item.Identity);
            var unchanged = previousState.PublishedApps.TryGetValue(item.Identity, out var previousApp)
                && ReferenceEquals(app, previousApp);
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
                new AppCatalogSnapshot(visibleItems.AsReadOnly(), hiddenItems.AsReadOnly(), patternHiddenItems.AsReadOnly(), commandAliases),
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
        var canonicalPackagedIdentities = BuildCanonicalPackagedIdentities(snapshots);
        var canonicalIdentityByTarget = BuildCanonicalIdentityByTarget(snapshots, canonicalPackagedIdentities);
        var merged = new Dictionary<string, AppCatalogItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var snapshot in snapshots.Values)
        {
            foreach (var item in snapshot)
            {
                if (string.IsNullOrWhiteSpace(item.Identity))
                {
                    continue;
                }

                var canonicalItem = CanonicalizeItem(item, canonicalIdentityByTarget, canonicalPackagedIdentities);
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

    private static Dictionary<string, string> BuildCanonicalPackagedIdentities(
        IReadOnlyDictionary<string, IReadOnlyList<AppCatalogItem>> snapshots)
    {
        var preferredByLaunch = new Dictionary<EdgePwaLaunchInfo, AppCatalogItem>();

        // Use the payload preference for PWA identity selection so activation and uninstall target the same package.
        foreach (var snapshot in snapshots.Values)
        {
            foreach (var item in snapshot)
            {
                if (item.Payload is PackagedAppPayload { EdgePwaLaunch: { IsSupported: true } launch }
                    && !string.IsNullOrWhiteSpace(item.Payload.GetCanonicalIdentityHint())
                    && (!preferredByLaunch.TryGetValue(launch, out var preferred)
                        || item.Provenance.ComparePreferenceTo(preferred.Provenance) < 0))
                {
                    preferredByLaunch[launch] = item;
                }
            }
        }

        var identities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var snapshot in snapshots.Values)
        {
            foreach (var item in snapshot)
            {
                if (item.Payload is PackagedAppPayload { EdgePwaLaunch: { } launch }
                    && item.Payload.GetCanonicalIdentityHint() is { } identity
                    && preferredByLaunch.TryGetValue(launch, out var preferred))
                {
                    identities[identity] = preferred.Payload.GetCanonicalIdentityHint()!;
                }
            }
        }

        return identities;
    }

    private static Dictionary<string, string?> BuildCanonicalIdentityByTarget(
        IReadOnlyDictionary<string, IReadOnlyList<AppCatalogItem>> snapshots,
        IReadOnlyDictionary<string, string> canonicalPackagedIdentities)
    {
        var canonicalIdentityByTarget = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var snapshot in snapshots.Values)
        {
            foreach (var item in snapshot)
            {
                var canonicalIdentity = GetCanonicalIdentityHint(item, canonicalPackagedIdentities);
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
        IReadOnlyDictionary<string, string?> canonicalIdentityByTarget,
        IReadOnlyDictionary<string, string> canonicalPackagedIdentities)
    {
        var canonicalIdentity = GetCanonicalIdentityHint(item, canonicalPackagedIdentities);
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

    private static string? GetCanonicalIdentityHint(
        AppCatalogItem item,
        IReadOnlyDictionary<string, string> canonicalPackagedIdentities)
    {
        var identity = item.Payload.GetCanonicalIdentityHint();
        return identity is not null && canonicalPackagedIdentities.TryGetValue(identity, out var canonicalIdentity)
            ? canonicalIdentity
            : identity;
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
