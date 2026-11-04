// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Sizes and places the notice, in physical pixels: just under the caret with its arrow pointing up at it, or above it
/// with the arrow pointing down when there's no room below, and always inside the monitor's work area. Pure, so tests
/// cover it without a window.
/// </summary>
internal static class NoticeLayout
{
    /// <summary>
    /// Gets the notice's measurements at one DPI, in physical pixels. The design gives them in device-independent pixels:
    /// a card 210 to 260 wide with 8-pixel corners and a 1-pixel outline, 12-pixel text beside a 14-pixel icon, an arrow
    /// 14 wide and 7 tall, and a flyout's shadow, which needs room around the card.
    /// </summary>
    internal readonly record struct Metrics(
        uint Dpi,
        int MinWidth,
        int MaxWidth,
        int Border,
        int PaddingX,
        int PaddingY,
        int TextSize,
        int IconSize,
        int IconGap,
        int CornerRadius,
        int ArrowHalfWidth,
        int ArrowHeight,
        int ArrowInset,
        int Gap,
        int ShadowLeft,
        int ShadowTop,
        int ShadowRight,
        int ShadowBottom)
    {
        /// <summary>
        /// Gets the measurements at <paramref name="dpi"/>. High contrast draws a heavier outline and no shadow, so the
        /// window only keeps a pixel around the card. Larger text (Accessibility &gt; Text size, up to 225%) makes the
        /// text, the icon and the card's width larger with it, so lines break in the same places.
        /// </summary>
        public static Metrics For(uint dpi, bool highContrast, double textScale = 1)
        {
            double text = Math.Clamp(double.IsFinite(textScale) ? textScale : 1, 1, 2.25);
            int border = Scale(highContrast ? 2 : 1, dpi);
            int shadowSide = highContrast ? 1 : Scale(16, dpi);
            return new Metrics(
                dpi,
                MinWidth: Scale(210 * text, dpi),
                MaxWidth: Scale(260 * text, dpi),
                Border: border,
                PaddingX: Scale(10, dpi) + border,
                PaddingY: Scale(8, dpi) + border,
                TextSize: Scale(12 * text, dpi),
                IconSize: Scale(14 * text, dpi),
                IconGap: Scale(8, dpi),
                CornerRadius: Scale(8, dpi),
                ArrowHalfWidth: Scale(7, dpi),
                ArrowHeight: Scale(7, dpi),
                ArrowInset: Scale(18, dpi),
                Gap: Scale(2, dpi),
                ShadowLeft: shadowSide,
                ShadowTop: highContrast ? 1 : Scale(8, dpi),
                ShadowRight: shadowSide,
                ShadowBottom: highContrast ? 1 : Scale(24, dpi));
        }
    }

    /// <summary>Where the notice goes.</summary>
    /// <param name="Window">The layered window, shadow included, in screen pixels.</param>
    /// <param name="Card">The card, from the window's top-left corner.</param>
    /// <param name="ArrowX">How far the arrow's tip is from the window's left edge.</param>
    /// <param name="ArrowY">How far the arrow's tip is from the window's top edge.</param>
    /// <param name="Below">Whether the notice is under the caret, with its arrow pointing up.</param>
    internal readonly record struct Placement(Native.RECT Window, Native.RECT Card, int ArrowX, int ArrowY, bool Below);

    /// <summary>Converts device-independent pixels to physical pixels at <paramref name="dpi"/>, rounding half away from zero.</summary>
    public static int Scale(double dips, uint dpi) => (int)Math.Round(dips * dpi / 96.0, MidpointRounding.AwayFromZero);

    /// <summary>Gets how far the text starts from the card's leading edge: after the padding, the icon and the gap.</summary>
    public static int TextInset(in Metrics metrics) => metrics.PaddingX + metrics.IconSize + metrics.IconGap;

    /// <summary>Gets the widest the text can be, in the widest card.</summary>
    public static int MaxTextWidth(in Metrics metrics) => metrics.MaxWidth - TextInset(metrics) - metrics.PaddingX;

    /// <summary>Gets the card's width for text whose longest line is <paramref name="textWidth"/> wide, within the design's range.</summary>
    public static int CardWidth(int textWidth, in Metrics metrics)
        => Math.Clamp(TextInset(metrics) + textWidth + metrics.PaddingX, metrics.MinWidth, metrics.MaxWidth);

    /// <summary>Gets the card's height for text <paramref name="textHeight"/> tall, never shorter than the icon's line.</summary>
    public static int CardHeight(int textHeight, int iconLineHeight, in Metrics metrics)
        => (metrics.PaddingY * 2) + Math.Max(textHeight, iconLineHeight);

    /// <summary>
    /// Places a card <paramref name="cardWidth"/> by <paramref name="cardHeight"/> at <paramref name="caret"/>. The arrow
    /// sits near the card's left end, as in the design, and points at the middle of the caret; when the work area's edge
    /// moves the card over, the arrow moves along it to keep pointing there, but never onto the rounded corners. The
    /// notice goes under the caret when it fits, else above it; when neither fits, it goes on the roomier side, moved
    /// into the work area.
    /// </summary>
    public static Placement Place(Native.RECT caret, int cardWidth, int cardHeight, Native.RECT workArea, in Metrics metrics)
    {
        int anchorX = caret.Left + (Math.Max(0, caret.Right - caret.Left) / 2);
        int height = metrics.ArrowHeight + cardHeight;

        int left = Math.Max(workArea.Left, Math.Min(anchorX - metrics.ArrowInset, workArea.Right - cardWidth));
        int nearest = metrics.CornerRadius + metrics.ArrowHalfWidth;
        int farthest = Math.Max(nearest, cardWidth - nearest);
        int arrowX = left + Math.Clamp(anchorX - left, nearest, farthest);

        int under = caret.Bottom + metrics.Gap;
        int over = caret.Top - metrics.Gap;
        bool below;
        int top;
        if (under + height <= workArea.Bottom)
        {
            below = true;
            top = under;
        }
        else if (over - height >= workArea.Top)
        {
            below = false;
            top = over - height;
        }
        else
        {
            below = workArea.Bottom - under >= over - workArea.Top;
            top = Math.Max(workArea.Top, Math.Min(below ? under : over - height, workArea.Bottom - height));
        }

        // The shadow stops at the work area's edge, so it never lies over the taskbar or another monitor. The card and
        // its arrow keep their place.
        int cardTop = below ? top + metrics.ArrowHeight : top;
        var window = new Native.RECT
        {
            Left = Math.Max(left - metrics.ShadowLeft, Math.Min(workArea.Left, left)),
            Top = Math.Max(top - metrics.ShadowTop, Math.Min(workArea.Top, top)),
            Right = Math.Min(left + cardWidth + metrics.ShadowRight, Math.Max(workArea.Right, left + cardWidth)),
            Bottom = Math.Min(top + height + metrics.ShadowBottom, Math.Max(workArea.Bottom, top + height)),
        };

        var card = new Native.RECT
        {
            Left = left - window.Left,
            Top = cardTop - window.Top,
            Right = left - window.Left + cardWidth,
            Bottom = cardTop - window.Top + cardHeight,
        };

        return new Placement(window, card, arrowX - window.Left, (below ? top : top + height) - window.Top, below);
    }
}
