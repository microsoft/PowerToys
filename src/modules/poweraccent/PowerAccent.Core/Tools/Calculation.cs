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

        public static Point GetRawCoordinatesFromPosition(Position position, Rect screen, Size window)
        {
            int offset = 24;

            double pointX = position switch
            {
                Position.Top or Position.Bottom or Position.Center
                    => screen.X + (screen.Width / 2) - (window.Width / 2),
                Position.TopLeft or Position.Left or Position.BottomLeft
                    => screen.X + offset,
                Position.TopRight or Position.Right or Position.BottomRight
                    => screen.X + screen.Width - (window.Width + offset),
                _ => throw new NotImplementedException(),
            };

            double pointY = position switch
            {
                Position.TopLeft or Position.Top or Position.TopRight
                    => screen.Y + offset,
                Position.Left or Position.Center or Position.Right
                    => screen.Y + (screen.Height / 2) - (window.Height / 2),
                Position.BottomLeft or Position.Bottom or Position.BottomRight
                    => screen.Y + screen.Height - (window.Height + offset),
                _ => throw new NotImplementedException(),
            };

            return new Point(pointX, pointY);
        }
    }
}
