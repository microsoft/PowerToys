// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Draws the notice into a bitmap that the layered window shows as it is: the icon, the bold heading and the message,
/// drawn with GDI on the card's color, then the card's shape, outline and shadow (<see cref="NoticeRenderer"/>). Fonts
/// are made for each monitor's DPI. Used on the notice's thread only.
/// </summary>
internal sealed class NoticePainter : IDisposable
{
    // Info, in Segoe Fluent Icons and, on Windows 10, at the same code point in Segoe MDL2 Assets.
    private const string InfoGlyph = "\uE946";

    // GDI quietly puts another font in place of a face it doesn't have, which for the icon would draw a box, so each
    // face is checked before it's used. Segoe UI Variable is Windows 11's; Windows 10 has Segoe UI.
    private static readonly string[] TextFaces = ["Segoe UI Variable Text", "Segoe UI"];
    private static readonly string[] HeadingFaces = ["Segoe UI Variable Text Semibold", "Segoe UI Semibold", "Segoe UI"];
    private static readonly string[] IconFaces = ["Segoe Fluent Icons", "Segoe MDL2 Assets"];

    private readonly char[] _faceName = new char[64];
    private IntPtr _measure;
    private int _fontTextSize;
    private int _fontIconSize;
    private IntPtr _textFont;
    private IntPtr _headingFont;
    private IntPtr _iconFont;

    /// <summary>
    /// Draws a notice with <paramref name="heading"/> and <paramref name="message"/> for a caret at
    /// <paramref name="caret"/>, placed inside <paramref name="workArea"/>, at <paramref name="dpi"/>, with text
    /// <paramref name="textScale"/> times its usual size.
    /// </summary>
    public Frame Paint(string heading, string message, Native.RECT caret, Native.RECT workArea, uint dpi, double textScale, NoticeTheme.Palette palette, bool rightToLeft)
    {
        NoticeLayout.Metrics metrics = NoticeLayout.Metrics.For(dpi, palette.HighContrast, textScale);
        EnsureFonts(metrics);
        uint wrap = Native.DT_WORDBREAK | Native.DT_NOPREFIX | (rightToLeft ? Native.DT_RIGHT | Native.DT_RTLREADING : 0);
        uint line = Native.DT_SINGLELINE | Native.DT_NOPREFIX;

        // As wide as the longest line needs, within the design's range and the screen; then as tall as the text is.
        int widest = NoticeLayout.MaxTextWidth(metrics);
        int workWidth = workArea.Right - workArea.Left;
        int cardWidth = Math.Min(NoticeLayout.CardWidth(Math.Max(Measure(_headingFont, heading, widest, wrap).Width, Measure(_textFont, message, widest, wrap).Width), metrics), Math.Max(1, workWidth));
        int textWidth = Math.Max(1, cardWidth - NoticeLayout.TextInset(metrics) - metrics.PaddingX);
        int headingHeight = Measure(_headingFont, heading, textWidth, wrap).Height;
        int messageHeight = Measure(_textFont, message, textWidth, wrap).Height;
        int lineHeight = Measure(_headingFont, "Ag", widest, line).Height;
        int iconHeight = Measure(_iconFont, InfoGlyph, widest, line).Height;
        int workHeight = workArea.Bottom - workArea.Top;
        int cardHeight = Math.Min(NoticeLayout.CardHeight(headingHeight + messageHeight, Math.Max(lineHeight, iconHeight), metrics), Math.Max(1, workHeight - metrics.ArrowHeight));

        NoticeLayout.Placement placement = NoticeLayout.Place(caret, cardWidth, cardHeight, workArea, metrics);
        Native.RECT card = placement.Card;
        var frame = new Frame(placement.Window.Right - placement.Window.Left, placement.Window.Bottom - placement.Window.Top, new Native.POINT { X = placement.Window.Left, Y = placement.Window.Top });
        try
        {
            Span<uint> pixels = frame.Pixels;
            pixels.Fill(palette.Fill);
            _ = Native.SetBkMode(frame.DC, Native.TRANSPARENT);

            // The icon is on the leading side, centered on the heading's first line.
            int top = card.Top + metrics.PaddingY;
            int textBottom = card.Bottom - metrics.PaddingY;
            int iconLeft = rightToLeft ? card.Right - metrics.PaddingX - metrics.IconSize : card.Left + metrics.PaddingX;
            int iconTop = top + ((lineHeight - iconHeight) / 2);
            int textLeft = rightToLeft ? card.Left + metrics.PaddingX : card.Left + NoticeLayout.TextInset(metrics);
            Draw(frame.DC, _iconFont, palette.Icon, InfoGlyph, new Native.RECT { Left = iconLeft, Top = iconTop, Right = iconLeft + metrics.IconSize, Bottom = iconTop + iconHeight }, line | Native.DT_CENTER | Native.DT_NOCLIP);
            Draw(frame.DC, _headingFont, palette.Text, heading, new Native.RECT { Left = textLeft, Top = top, Right = textLeft + textWidth, Bottom = Math.Min(top + headingHeight, textBottom) }, wrap);
            Draw(frame.DC, _textFont, palette.Text, message, new Native.RECT { Left = textLeft, Top = top + headingHeight, Right = textLeft + textWidth, Bottom = Math.Min(top + headingHeight + messageHeight, textBottom) }, wrap);
            _ = Native.GdiFlush();

            var shape = new NoticeRenderer.Shape(card.Left, card.Top, card.Right, card.Bottom, metrics.CornerRadius, placement.ArrowX, placement.ArrowY, metrics.ArrowHalfWidth, metrics.ArrowHeight, placement.Below, metrics.Border);
            NoticeRenderer.Compose(pixels, frame.Width, frame.Height, shape, palette.Border, Shadows(dpi, palette));
            return frame;
        }
        catch
        {
            frame.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        DeleteFonts();
        if (_measure != IntPtr.Zero)
        {
            _ = Native.DeleteDC(_measure);
            _measure = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Gets a flyout's shadow, as CSS writes it: 0 8px 16px and 0 0 2px, darker in the dark theme. High contrast has
    /// none: its outline sets the notice apart.
    /// </summary>
    internal static NoticeRenderer.Shadow[] Shadows(uint dpi, NoticeTheme.Palette palette)
    {
        if (palette.HighContrast)
        {
            return [];
        }

        float scale = dpi / 96f;
        return [new(8 * scale, 8 * scale, palette.Dark ? 0.28f : 0.14f), new(0, 1 * scale, palette.Dark ? 0.24f : 0.14f)];
    }

    private static void Draw(IntPtr dc, IntPtr font, uint color, string text, Native.RECT bounds, uint format)
    {
        if (text.Length == 0 || bounds.Bottom <= bounds.Top)
        {
            return;
        }

        IntPtr previous = Native.SelectObject(dc, font);
        _ = Native.SetTextColor(dc, NoticeTheme.SwapRedAndBlue(color));
        _ = Native.DrawTextW(dc, text, text.Length, ref bounds, format);
        _ = Native.SelectObject(dc, previous);
    }

    private (int Width, int Height) Measure(IntPtr font, string text, int width, uint format)
    {
        if (text.Length == 0)
        {
            return (0, 0);
        }

        IntPtr previous = Native.SelectObject(_measure, font);
        var bounds = new Native.RECT { Right = width };
        _ = Native.DrawTextW(_measure, text, text.Length, ref bounds, format | Native.DT_CALCRECT);
        _ = Native.SelectObject(_measure, previous);

        // A word longer than the line isn't broken, and is cut off at the card's edge.
        return (Math.Min(bounds.Right - bounds.Left, width), bounds.Bottom - bounds.Top);
    }

    private void EnsureFonts(in NoticeLayout.Metrics metrics)
    {
        if (_measure == IntPtr.Zero)
        {
            _measure = Native.CreateCompatibleDC(IntPtr.Zero);
            if (_measure == IntPtr.Zero)
            {
                throw new InvalidOperationException("Windows didn't give the notice a device context to measure its text.");
            }
        }

        if (_fontTextSize == metrics.TextSize && _fontIconSize == metrics.IconSize && _textFont != IntPtr.Zero)
        {
            return;
        }

        DeleteFonts();
        _textFont = CreateFont(TextFaces, metrics.TextSize, Native.FW_NORMAL);
        _headingFont = CreateFont(HeadingFaces, metrics.TextSize, Native.FW_SEMIBOLD);
        _iconFont = CreateFont(IconFaces, metrics.IconSize, Native.FW_NORMAL);
        _fontTextSize = metrics.TextSize;
        _fontIconSize = metrics.IconSize;
    }

    // The first of the faces Windows has, or GDI's stand-in for the last.
    private IntPtr CreateFont(string[] faces, int pixels, int weight)
    {
        IntPtr font = IntPtr.Zero;
        foreach (string face in faces)
        {
            if (font != IntPtr.Zero)
            {
                _ = Native.DeleteObject(font);
            }

            font = Native.CreateFontW(-pixels, 0, 0, 0, weight, 0, 0, 0, Native.DEFAULT_CHARSET, 0, 0, 0, 0, face);
            if (font != IntPtr.Zero && HasFace(font, face))
            {
                return font;
            }
        }

        return font != IntPtr.Zero ? font : throw new InvalidOperationException("Windows didn't give the notice a font.");
    }

    private bool HasFace(IntPtr font, string face)
    {
        IntPtr previous = Native.SelectObject(_measure, font);
        int length = Native.GetTextFaceW(_measure, _faceName.Length, _faceName);
        _ = Native.SelectObject(_measure, previous);
        return length > 0 && string.Equals(new string(_faceName, 0, Math.Min(length, _faceName.Length)).TrimEnd('\0'), face, StringComparison.OrdinalIgnoreCase);
    }

    private void DeleteFonts()
    {
        foreach (IntPtr font in (ReadOnlySpan<IntPtr>)[_textFont, _headingFont, _iconFont])
        {
            if (font != IntPtr.Zero)
            {
                _ = Native.DeleteObject(font);
            }
        }

        _textFont = _headingFont = _iconFont = IntPtr.Zero;
        _fontTextSize = _fontIconSize = 0;
    }

    /// <summary>A drawn notice: a 32-bit top-down bitmap selected into a memory DC, and where on the screen it goes.</summary>
    internal sealed class Frame : IDisposable
    {
        private IntPtr _bitmap;
        private IntPtr _previous;
        private IntPtr _bits;

        public Frame(int width, int height, Native.POINT position)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            Position = position;
            DC = Native.CreateCompatibleDC(IntPtr.Zero);
            var header = new Native.BITMAPINFOHEADER
            {
                BiSize = (uint)Marshal.SizeOf<Native.BITMAPINFOHEADER>(),
                BiWidth = Width,
                BiHeight = -Height,
                BiPlanes = 1,
                BiBitCount = 32,
            };
            _bitmap = DC == IntPtr.Zero ? IntPtr.Zero : Native.CreateDIBSection(DC, ref header, 0, out _bits, IntPtr.Zero, 0);
            if (_bitmap == IntPtr.Zero || _bits == IntPtr.Zero)
            {
                Dispose();
                throw new InvalidOperationException("Windows didn't give the notice a bitmap to draw in.");
            }

            _previous = Native.SelectObject(DC, _bitmap);
        }

        public IntPtr DC { get; private set; }

        public int Width { get; }

        public int Height { get; }

        public Native.POINT Position { get; }

        public Native.SIZE Size => new() { Cx = Width, Cy = Height };

        public unsafe Span<uint> Pixels => _bits == IntPtr.Zero ? Span<uint>.Empty : new Span<uint>((void*)_bits, Width * Height);

        public void Dispose()
        {
            if (DC != IntPtr.Zero)
            {
                if (_previous != IntPtr.Zero)
                {
                    _ = Native.SelectObject(DC, _previous);
                }

                _ = Native.DeleteDC(DC);
                DC = IntPtr.Zero;
            }

            if (_bitmap != IntPtr.Zero)
            {
                _ = Native.DeleteObject(_bitmap);
                _bitmap = IntPtr.Zero;
            }

            _bits = IntPtr.Zero;
            _previous = IntPtr.Zero;
        }
    }
}
