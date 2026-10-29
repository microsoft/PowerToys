// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.Foundation;

namespace Microsoft.PowerToys.TextExpander.Editor.Helpers
{
    /// <summary>
    /// Where the question flyout goes, in window coordinates. It is as wide as the Replacement box and lined up
    /// with it, so it covers the buttons under the box completely or not at all. It goes <see cref="Gap"/> under
    /// the token's line, or above the line when it fits only there, and never over it. When it fits neither way,
    /// it goes where there's more room, without its preview, and scrolls if it still doesn't fit.
    /// </summary>
    /// <param name="X">The flyout's left edge.</param>
    /// <param name="Y">The flyout's top edge, or its bottom edge when it goes <paramref name="Above"/> the token.</param>
    /// <param name="Width">The flyout's width.</param>
    /// <param name="MaxHeight">The most height it can take without covering the token's line or leaving the window.</param>
    /// <param name="Above">Whether it goes above the token's line.</param>
    /// <param name="ShowsPreview">Whether there's room for its preview.</param>
    internal readonly record struct TextExpanderPromptPlacement(double X, double Y, double Width, double MaxHeight, bool Above, bool ShowsPreview)
    {
        /// <summary>The space between the flyout and the token's line, and between the flyout and the window's edges.</summary>
        public const double Gap = 4;

        /// <summary>Gets the flyout's width: the Replacement box's, within the window.</summary>
        public static double WidthFor(Rect box, Size window) => Math.Max(0, Math.Min(box.Width, window.Width - (2 * Gap)));

        /// <summary>Places a flyout that is <paramref name="height"/> tall, with its preview, at <see cref="WidthFor"/>.</summary>
        public static TextExpanderPromptPlacement Place(Rect token, Rect box, Size window, double height)
        {
            double width = WidthFor(box, window);
            double x = Math.Clamp(box.X, Gap, Math.Max(Gap, window.Width - Gap - width));
            double below = token.Bottom + Gap;
            double above = token.Top - Gap;
            double roomBelow = window.Height - Gap - below;
            double roomAbove = above - Gap;

            bool showsPreview = height <= roomBelow || height <= roomAbove;
            bool isAbove = showsPreview ? height > roomBelow : roomAbove > roomBelow;
            return new TextExpanderPromptPlacement(x, isAbove ? above : below, width, Math.Max(0, isAbove ? roomAbove : roomBelow), isAbove, showsPreview);
        }
    }
}
