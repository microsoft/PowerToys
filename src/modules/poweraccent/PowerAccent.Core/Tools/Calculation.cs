// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerAccent.Core.Services;

namespace PowerAccent.Core.Tools
{
    internal static class Calculation
    {
        public static Point GetRawCoordinatesFromCaret(Point caret, Rect screen, Size window)
        {
            double left = caret.X - (window.Width / 2);
            double top = caret.Y - window.Height - 20;

            return new Point(
                left < screen.X ? screen.X : (left + window.Width > (screen.X + screen.Width) ? (screen.X + screen.Width) - window.Width : left),
                top < screen.Y ? caret.Y + 20 : top);
        }

        public static Point GetRawCoordinatesFromPosition(Position position, Rect screen, Size window, double dpi)
        {
            int offset = 24;

            double pointX = position switch
            {
                Position.Top or Position.Bottom or Position.Center
                    => screen.X + (screen.Width / 2) - (window.Width * dpi / 2),
                Position.TopLeft or Position.Left or Position.BottomLeft
                    => screen.X + offset,
                Position.TopRight or Position.Right or Position.BottomRight
                    => screen.X + screen.Width - ((window.Width * dpi) + offset),
                _ => throw new NotImplementedException(),
            };

            double pointY = position switch
            {
                Position.TopLeft or Position.Top or Position.TopRight
                    => screen.Y + offset,
                Position.Left or Position.Center or Position.Right
                    => screen.Y + (screen.Height / 2) - (window.Height * dpi / 2),
                Position.BottomLeft or Position.Bottom or Position.BottomRight
                    => screen.Y + screen.Height - ((window.Height * dpi) + offset),
                _ => throw new NotImplementedException(),
            };

            return new Point(pointX, pointY);
        }

        /// <summary>
        /// Calculates the width of the accent bar window, in DIP.
        /// </summary>
        /// <param name="measuredContentWidth">Width the character list reported when measured against
        /// an unbounded width, in DIP; 0 when it could not be measured yet.</param>
        /// <param name="itemCount">Number of accent characters in the bar.</param>
        /// <param name="minItemWidth">Minimum width of a single accent cell, in DIP.</param>
        /// <param name="chromeWidth">Width of everything outside the character list - the surface's
        /// outer margin and border, plus a rounding allowance - in DIP.</param>
        /// <param name="descriptionMinWidth">Minimum width of the whole bar while the Unicode
        /// description row is shown, in DIP; 0 while that row is hidden.</param>
        /// <param name="maxWidth">Widest bar the active display can take, in DIP. Character sets that
        /// need more than this scroll instead of growing past it.</param>
        public static double GetToolbarWidth(
            double measuredContentWidth,
            int itemCount,
            double minItemWidth,
            double chromeWidth,
            double descriptionMinWidth,
            double maxWidth)
        {
            // Every cell is at least minItemWidth wide, so itemCount * minItemWidth is a lower bound
            // the measurement can only exceed. Taking the larger of the two lets a glyph that is
            // wider than the cell minimum widen the window instead of silently overflowing into the
            // list's scroll viewer, while a list that could not be measured (it reports 0) still
            // falls back to the estimate rather than collapsing the bar.
            double contentWidth = Math.Max(measuredContentWidth, itemCount * minItemWidth);
            double width = Math.Max(contentWidth + chromeWidth, descriptionMinWidth);

            // One cell plus the chrome is the narrowest bar that can still draw a glyph: the chrome
            // is the surface's own margin and border, so flooring at minItemWidth alone would
            // describe a window whose entire client area is margin. Math.Max on the upper bound
            // because a display narrower than that floor would otherwise invert the clamp bounds
            // and throw (Math.Clamp documents ArgumentException when max is less than min).
            double floorWidth = minItemWidth + chromeWidth;
            return Math.Clamp(width, floorWidth, Math.Max(floorWidth, maxWidth));
        }
    }
}
