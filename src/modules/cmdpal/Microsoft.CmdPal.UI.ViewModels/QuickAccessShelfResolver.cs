// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

internal static class QuickAccessShelfResolver
{
    internal sealed record ResolvedItem<T>(T Item, bool IsPinned, int ShortcutIndex, bool StartsNewSection);

    internal static string IndexToShortcutDigit(int index)
        => NumberedItemShortcuts.IndexToShortcutDigit(index);

    internal static int CalculateVisibleCapacity(
        int itemCount,
        double availableWidth,
        double itemWidth,
        double spacing)
    {
        if (itemCount <= 0 || availableWidth <= 0 || itemWidth <= 0 || spacing < 0)
        {
            return 0;
        }

        static int CountThatFits(double width, double itemWidth, double spacing) =>
            Math.Max(0, (int)Math.Floor((width + spacing) / (itemWidth + spacing)));

        var capacityWithoutOverflow = CountThatFits(availableWidth, itemWidth, spacing);
        if (itemCount <= capacityWithoutOverflow)
        {
            return itemCount;
        }

        // Reserve one item-width slot for the overflow button plus the gap before it.
        var widthBeforeOverflow = Math.Max(0, availableWidth - itemWidth - spacing);
        return Math.Min(itemCount, CountThatFits(widthBeforeOverflow, itemWidth, spacing));
    }

    internal static IReadOnlyList<ResolvedItem<T>> ComposeSections<T>(
        IReadOnlyList<T> pinnedItems,
        IReadOnlyList<T> recentItems,
        RecentCommandsPlacement recentCommandsPlacement)
    {
        var pinnedCount = pinnedItems.Count;
        var includeRecentCommands = recentCommandsPlacement is
            RecentCommandsPlacement.BeforePinned or RecentCommandsPlacement.AfterPinned;
        var recentCount = includeRecentCommands ? recentItems.Count : 0;
        var result = new List<ResolvedItem<T>>(pinnedCount + recentCount);

        void AppendPinned(bool startsNewSection)
        {
            for (var i = 0; i < pinnedCount; i++)
            {
                result.Add(new ResolvedItem<T>(pinnedItems[i], true, result.Count, startsNewSection && i == 0));
            }
        }

        void AppendRecent(bool startsNewSection)
        {
            for (var i = 0; i < recentCount; i++)
            {
                result.Add(new ResolvedItem<T>(recentItems[i], false, result.Count, startsNewSection && i == 0));
            }
        }

        if (recentCommandsPlacement == RecentCommandsPlacement.BeforePinned)
        {
            AppendRecent(startsNewSection: false);
            AppendPinned(startsNewSection: recentCount > 0);
        }
        else
        {
            AppendPinned(startsNewSection: false);
            AppendRecent(startsNewSection: pinnedCount > 0);
        }

        return result;
    }
}
