// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using Windows.UI.Palettes;

namespace Microsoft.CmdPal.Ext.WindowsPalettes;

/// <summary>
/// Reports item use to the Windows.UI.Palettes provider.
/// Reports each returned item once per session and records the selected item.
/// At session end, reports whether each item was selected, another item was selected, or no item was selected.
/// </summary>
internal sealed class WupAnalyticsSession(IPaletteItemSourceExtension extension)
{
    private const char KeySeparator = '\u001F';
    private readonly ConcurrentDictionary<string, byte> _visibleItemIds = new(StringComparer.Ordinal);
    private string? _selectedItemId;

    internal async Task ReportVisibleAsync(IReadOnlyList<PaletteItemView>? items)
    {
        if (items is null || items.Count == 0)
        {
            return;
        }

        List<PaletteItemAnalytics> reports = [];
        foreach (var item in items)
        {
            var key = $"{item.ExtensionId}{KeySeparator}{item.Id}";
            if (_visibleItemIds.TryAdd(key, 0))
            {
                reports.Add(new PaletteItemAnalytics(item.Id, item.ExtensionId, DateTimeOffset.UtcNow));
            }
        }

        if (reports.Count > 0)
        {
            await extension.ReportItemAnalyticsAsync([.. reports]);
        }
    }

    internal void RecordSelection(PaletteItemView item) =>
        _selectedItemId = $"{item.ExtensionId}{KeySeparator}{item.Id}";

    internal async Task ReportConversationAsync(bool itemSelectedElsewhere)
    {
        var selectedItemId = Interlocked.Exchange(ref _selectedItemId, null);
        if (_visibleItemIds.IsEmpty)
        {
            return;
        }

        var keys = _visibleItemIds.Keys.ToArray();
        _visibleItemIds.Clear();
        var reports = new PaletteItemAnalytics[keys.Length];
        var index = 0;
        foreach (var key in keys)
        {
            var separator = key.IndexOf(KeySeparator, StringComparison.Ordinal);
            var extensionId = key[..separator];
            var itemId = key[(separator + 1)..];
            var outcome = string.Equals(key, selectedItemId, StringComparison.Ordinal)
                ? PaletteItemAnalyticsOutcome.Selected
                : selectedItemId is not null || itemSelectedElsewhere
                    ? PaletteItemAnalyticsOutcome.OtherSelected
                    : PaletteItemAnalyticsOutcome.Dismissed;
            reports[index++] = new PaletteItemAnalytics(itemId, extensionId, DateTimeOffset.UtcNow)
            {
                Outcome = outcome,
            };
        }

        await extension.ReportConversationAnalyticsAsync(reports);
    }
}
