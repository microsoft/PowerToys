// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.Foundation;

namespace Microsoft.PowerToys.TextExpander.Editor.Helpers
{
    /// <summary>
    /// Where the question flyout goes, in window coordinates. It lines up with the Replacement box, except that a box
    /// starting above Save, as the dialog's right column does, gets a flyout starting where Cancel does, so Save stays
    /// whole in view. It goes <see cref="Gap"/> under the token's line, or above the line when it fits only there, and
    /// never over it. When it fits neither way, it goes where there's more room, without its preview, and scrolls if it
    /// still doesn't fit. It never cuts through Save, Cancel or Insert variable: it covers one it spans from side to side
    /// completely or stops clear of it, and stops clear of one it would only partly hide.
    /// </summary>
    /// <param name="X">The flyout's left edge.</param>
    /// <param name="Y">The flyout's top edge, or its bottom edge when it goes <paramref name="Above"/> the token.</param>
    /// <param name="Width">The flyout's width.</param>
    /// <param name="MinHeight">The height it needs to cover a control it would otherwise cut through, or 0.</param>
    /// <param name="MaxHeight">The most height it can take without covering the token's line, cutting through a control, or leaving the window.</param>
    /// <param name="Above">Whether it goes above the token's line.</param>
    /// <param name="ShowsPreview">Whether there's room for its preview.</param>
    internal readonly record struct TextExpanderPromptPlacement(double X, double Y, double Width, double MinHeight, double MaxHeight, bool Above, bool ShowsPreview)
    {
        /// <summary>The space between the flyout and the token's line, the window's edges, and a control it stops clear of.</summary>
        public const double Gap = 4;

        /// <summary>Gets the flyout's left edge and width: the Replacement box's, from where Cancel starts when the box starts above Save, within the window.</summary>
        public static (double X, double Width) Span(Rect box, Rect save, Rect cancel, Size window)
        {
            double left = box.X > save.X && box.X < cancel.X ? cancel.X : box.X;
            double width = Math.Max(0, Math.Min(box.Right - left, window.Width - (2 * Gap)));
            return (Math.Clamp(left, Gap, Math.Max(Gap, window.Width - Gap - width)), width);
        }

        /// <summary>
        /// Places a flyout that is <paramref name="height"/> tall with its preview and <paramref name="compactHeight"/>
        /// without it, at its <see cref="Span"/>. An empty <see cref="Rect"/> stands for a control that isn't there.
        /// </summary>
        public static TextExpanderPromptPlacement Place(Rect token, Rect box, Size window, double height, double compactHeight, Rect save, Rect cancel, Rect insertVariable)
        {
            Rect[] keepWhole = { save, cancel, insertVariable };
            (double x, double width) = Span(box, save, cancel, window);
            double below = token.Bottom + Gap;
            double above = token.Top - Gap;
            double roomBelow = window.Height - Gap - below;
            double roomAbove = above - Gap;
            foreach (Rect control in keepWhole)
            {
                // The flyout can't hide a control it only partly spans, so it has to end before it.
                if (control.Right > x && control.Left < x + width && (control.Left < x || control.Right > x + width))
                {
                    roomBelow = control.Top >= below ? Math.Min(roomBelow, control.Top - Gap - below) : roomBelow;
                    roomAbove = control.Bottom <= above ? Math.Min(roomAbove, above - control.Bottom - Gap) : roomAbove;
                }
            }

            bool showsPreview = height <= roomBelow || height <= roomAbove;
            bool isAbove = showsPreview ? height > roomBelow : roomAbove > roomBelow;
            double room = Math.Max(0, isAbove ? roomAbove : roomBelow);
            double extent = Math.Min(showsPreview ? height : compactHeight, room);
            double minHeight = 0;
            double maxHeight = room;
            foreach (Rect control in keepWhole)
            {
                // How far the flyout's far edge reaches into a control it spans, and how far it has to reach to cover it.
                double into = isAbove ? control.Bottom - (above - extent) : below + extent - control.Top;
                double through = isAbove ? above - control.Top : control.Bottom - below;
                if (control.Left < x || control.Right > x + width || into <= 0 || extent >= through)
                {
                    continue;
                }

                if (through + Gap <= room)
                {
                    extent = minHeight = through + Gap;
                }
                else
                {
                    extent = maxHeight = Math.Max(0, extent - into - Gap);
                }
            }

            return new TextExpanderPromptPlacement(x, isAbove ? above : below, width, minHeight, maxHeight, isAbove, showsPreview);
        }
    }
}
