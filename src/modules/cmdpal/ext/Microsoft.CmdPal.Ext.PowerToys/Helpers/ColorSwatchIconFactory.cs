// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PowerToysExtension.Helpers;

internal static class ColorSwatchIconFactory
{
    public static IconInfo Create(byte r, byte g, byte b, byte a)
    {
        try
        {
            using var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
            using var gfx = Graphics.FromImage(bmp);
            gfx.SmoothingMode = SmoothingMode.AntiAlias;
            gfx.Clear(Color.Transparent);

            using var brush = new SolidBrush(Color.FromArgb(a, r, g, b));
            const int padding = 4;
            gfx.FillEllipse(brush, padding, padding, bmp.Width - (padding * 2), bmp.Height - (padding * 2));

            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            var iconData = IconInfo.FromStream(ms.AsRandomAccessStream()).Dark as IconData;
            return iconData != null ? new IconInfo(iconData, iconData) : new IconInfo("\u25CF");
        }
        catch
        {
            // Fallback to a simple colored glyph when drawing fails.
            return new IconInfo("\u25CF"); // Black circle glyph
        }
    }
}
