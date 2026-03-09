// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

internal sealed class FallbackSnapshotItemCache
{
    private readonly Lock _lock = new();
    private readonly AppExtensionHost _extensionHost;
    private readonly ICommandProviderContext _providerContext;
    private readonly string _fallbackSourceId;
    private readonly string _extensionName;
    private Dictionary<string, FallbackQueryResultItem> _currentItemsByIdentity = new(StringComparer.Ordinal);

    internal FallbackSnapshotItemCache(
        AppExtensionHost extensionHost,
        ICommandProviderContext providerContext,
        string fallbackSourceId,
        string extensionName)
    {
        _extensionHost = extensionHost;
        _providerContext = providerContext;
        _fallbackSourceId = fallbackSourceId;
        _extensionName = extensionName;
    }

    internal IListItem[] Materialize(
        IReadOnlyList<FallbackSnapshotDefinition> snapshotItems,
        string aliasText,
        bool hasAlias,
        IFallbackCommandInvocationArgs? invocationArgs,
        Func<bool> isCurrent)
    {
        lock (_lock)
        {
            if (snapshotItems.Count == 0)
            {
                ClearUnderLock();
                return [];
            }

            var identityCounts = new Dictionary<string, int>(snapshotItems.Count, StringComparer.Ordinal);
            var nextItemsByIdentity = new Dictionary<string, FallbackQueryResultItem>(snapshotItems.Count, StringComparer.Ordinal);
            var materializedItems = new IListItem[snapshotItems.Count];

            for (var i = 0; i < snapshotItems.Count; i++)
            {
                var snapshotItem = snapshotItems[i];
                var identity = CreateIdentity(snapshotItem, identityCounts);

                if (_currentItemsByIdentity.TryGetValue(identity, out var existingItem))
                {
                    existingItem.RefreshFromSource(
                        snapshotItem.SourceItem,
                        aliasText,
                        hasAlias,
                        invocationArgs,
                        isCurrent,
                        snapshotItem.ListenForSourceItemUpdates,
                        snapshotItem.TitleOverride,
                        snapshotItem.SubtitleOverride);

                    nextItemsByIdentity[identity] = existingItem;
                    materializedItems[i] = existingItem;
                    continue;
                }

                var item = new FallbackQueryResultItem(
                    identity,
                    snapshotItem.SourceItem,
                    _extensionHost,
                    _providerContext,
                    _fallbackSourceId,
                    _extensionName,
                    aliasText,
                    hasAlias,
                    invocationArgs,
                    isCurrent,
                    snapshotItem.ListenForSourceItemUpdates,
                    snapshotItem.TitleOverride,
                    snapshotItem.SubtitleOverride);

                nextItemsByIdentity[identity] = item;
                materializedItems[i] = item;
            }

            foreach (var pair in _currentItemsByIdentity)
            {
                if (!nextItemsByIdentity.ContainsKey(pair.Key))
                {
                    pair.Value.DetachSourceItemUpdates();
                }
            }

            _currentItemsByIdentity = nextItemsByIdentity;
            return materializedItems;
        }
    }

    internal void Clear()
    {
        lock (_lock)
        {
            ClearUnderLock();
        }
    }

    private void ClearUnderLock()
    {
        foreach (var item in _currentItemsByIdentity.Values)
        {
            item.DetachSourceItemUpdates();
        }

        _currentItemsByIdentity.Clear();
    }

    private string CreateIdentity(FallbackSnapshotDefinition snapshotItem, Dictionary<string, int> identityCounts)
    {
        var baseIdentity = BuildIdentity(snapshotItem.SourceItem, snapshotItem.TitleOverride, snapshotItem.SubtitleOverride);
        identityCounts.TryGetValue(baseIdentity, out var currentCount);
        currentCount++;
        identityCounts[baseIdentity] = currentCount;

        return currentCount == 1
            ? baseIdentity
            : $"{baseIdentity}#{currentCount.ToString(CultureInfo.InvariantCulture)}";
    }

    private string BuildIdentity(IListItem sourceItem, string? titleOverride, string? subtitleOverride)
    {
        var sourceIdentity = sourceItem is IObjectWithIdentity identifiable && !string.IsNullOrWhiteSpace(identifiable.Identity)
            ? identifiable.Identity
            : sourceItem.Command is { Id: { Length: > 0 } commandId }
                ? commandId
                : BuildSyntheticIdentity(sourceItem, titleOverride, subtitleOverride);

        return $"{_fallbackSourceId}:{sourceIdentity}";
    }

    private static string BuildSyntheticIdentity(IListItem sourceItem, string? titleOverride, string? subtitleOverride)
    {
        var title = titleOverride ?? sourceItem.Title ?? string.Empty;
        var subtitle = subtitleOverride ?? sourceItem.Subtitle ?? string.Empty;
        var section = sourceItem.Section ?? string.Empty;
        var textToSuggest = sourceItem.TextToSuggest ?? string.Empty;
        return $"{sourceItem.GetType().FullName}|{title}|{subtitle}|{section}|{textToSuggest}";
    }
}
