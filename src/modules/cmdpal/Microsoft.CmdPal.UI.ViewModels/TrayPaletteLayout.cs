// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

public static class TrayPaletteLayout
{
    public const double TileWidth = 112;
    public const double TileHeight = 100;
    public const double SmallContentHeight = TileHeight;
    public const double Width = TileWidth * 3;

    public static Rect[] GetItemBounds(IEnumerable<bool> smallContent)
    {
        List<Rect> bounds = [];
        var column = 0;
        var top = 0.0;
        foreach (var fullRow in smallContent)
        {
            if (fullRow)
            {
                if (column != 0)
                {
                    top += TileHeight;
                    column = 0;
                }

                bounds.Add(new(0, top, Width, SmallContentHeight));
                top += SmallContentHeight;
            }
            else
            {
                bounds.Add(new(column * TileWidth, top, TileWidth, TileHeight));
                if (++column == 3)
                {
                    column = 0;
                    top += TileHeight;
                }
            }
        }

        return bounds.ToArray();
    }

    public static int GetInsertionIndex(IReadOnlyList<Rect> bounds, Point point)
    {
        for (var i = 0; i < bounds.Count; i++)
        {
            var item = bounds[i];
            if (point.Y < item.Top)
            {
                return i;
            }

            if (point.Y < item.Bottom)
            {
                if (item.Width == Width)
                {
                    return point.Y < item.Top + (item.Height / 2) ? i : i + 1;
                }

                if (point.X < item.Right)
                {
                    return point.X < item.Left + (item.Width / 2) ? i : i + 1;
                }
            }
        }

        return bounds.Count;
    }
}
