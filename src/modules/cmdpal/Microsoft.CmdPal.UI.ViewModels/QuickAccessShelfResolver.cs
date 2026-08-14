// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

internal static class QuickAccessShelfResolver
{
    internal static string IndexToShortcutDigit(int index)
    {
        return index switch
        {
            >= 0 and <= 8 => (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => string.Empty,
        };
    }

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
}
