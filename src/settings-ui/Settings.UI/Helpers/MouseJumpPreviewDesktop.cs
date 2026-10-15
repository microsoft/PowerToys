// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

using Microsoft.Win32;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>
    /// Builds the fake desktop image used for the Mouse Jump preview in Settings:
    /// the user's wallpaper on two screens, each with a Windows 11 style acrylic taskbar.
    /// </summary>
    internal static class MouseJumpPreviewDesktop
    {
        private const int TaskbarHeight = 36;

        private static readonly Lazy<Bitmap> LazyImage = new(CreateDesktopImage);

        public static Rectangle PrimaryScreen { get; } = new(0, 0, 960, 540);

        public static Rectangle SecondaryScreen { get; } = new(960, 180, 576, 360);

        public static Bitmap Image => LazyImage.Value;

        private static Bitmap CreateDesktopImage()
        {
            var bounds = Rectangle.Union(PrimaryScreen, SecondaryScreen);
            var desktop = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb);
            using var graphics = System.Drawing.Graphics.FromImage(desktop);
            graphics.Clear(Color.Transparent);

            using var wallpaper = LoadWallpaper();
            var lightTheme = IsSystemLightTheme();

            DrawScreen(graphics, PrimaryScreen, wallpaper, lightTheme, showTray: true);
            DrawScreen(graphics, SecondaryScreen, wallpaper, lightTheme, showTray: false);

            return desktop;
        }

        private static void DrawScreen(System.Drawing.Graphics target, Rectangle screen, Bitmap wallpaper, bool lightTheme, bool showTray)
        {
            using var screenImage = new Bitmap(screen.Width, screen.Height, PixelFormat.Format32bppPArgb);
            using (var graphics = System.Drawing.Graphics.FromImage(screenImage))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                // fill the screen with the wallpaper, cropping it to keep its aspect ratio
                var scale = Math.Max((float)screen.Width / wallpaper.Width, (float)screen.Height / wallpaper.Height);
                var sourceWidth = screen.Width / scale;
                var sourceHeight = screen.Height / scale;
                DrawImageClamped(
                    graphics,
                    wallpaper,
                    new Rectangle(0, 0, screen.Width, screen.Height),
                    new RectangleF((wallpaper.Width - sourceWidth) / 2, (wallpaper.Height - sourceHeight) / 2, sourceWidth, sourceHeight));

                DrawTaskbar(graphics, screenImage, lightTheme, showTray);
            }

            target.DrawImage(screenImage, screen);
        }

        private static void DrawTaskbar(System.Drawing.Graphics graphics, Bitmap screenImage, bool lightTheme, bool showTray)
        {
            var width = screenImage.Width;
            var height = screenImage.Height;
            var taskbar = new Rectangle(0, height - TaskbarHeight, width, TaskbarHeight);

            // acrylic: a heavily blurred copy of the wallpaper behind the taskbar, plus a tint
            const int blurFactor = 24;
            using (var blurred = new Bitmap(Math.Max(1, width / blurFactor), Math.Max(1, height / blurFactor), PixelFormat.Format32bppPArgb))
            {
                using (var blurGraphics = System.Drawing.Graphics.FromImage(blurred))
                {
                    blurGraphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    DrawImageClamped(blurGraphics, screenImage, new Rectangle(0, 0, blurred.Width, blurred.Height), new RectangleF(0, 0, width, height));
                }

                var ratio = (float)blurred.Height / height;
                DrawImageClamped(
                    graphics,
                    blurred,
                    taskbar,
                    new RectangleF(0, taskbar.Top * ratio, blurred.Width, taskbar.Height * ratio));
            }

            using (var tint = new SolidBrush(lightTheme ? Color.FromArgb(184, 238, 238, 238) : Color.FromArgb(200, 28, 28, 28)))
            {
                graphics.FillRectangle(tint, taskbar);
            }

            using (var stroke = new Pen(lightTheme ? Color.FromArgb(24, 0, 0, 0) : Color.FromArgb(32, 255, 255, 255)))
            {
                graphics.DrawLine(stroke, taskbar.Left, taskbar.Top, taskbar.Right, taskbar.Top);
            }

            // centered start button and pinned apps
            var iconSize = TaskbarHeight / 2;
            var slotWidth = (int)(TaskbarHeight * 0.85);
            const int appCount = 4;
            var foregroundColor = lightTheme ? Color.FromArgb(150, 0, 0, 0) : Color.FromArgb(190, 255, 255, 255);
            var slotCount = 1 + appCount;
            var slotsLeft = (width - (slotCount * slotWidth)) / 2;
            var iconTop = taskbar.Top + ((TaskbarHeight - iconSize) / 2);
            var indicatorColor = lightTheme ? Color.FromArgb(140, 0, 0, 0) : Color.FromArgb(160, 255, 255, 255);

            DrawStartLogo(graphics, new Rectangle(slotsLeft + ((slotWidth - iconSize) / 2), iconTop, iconSize, iconSize));
            for (var i = 0; i < appCount; i++)
            {
                var iconLeft = slotsLeft + ((i + 1) * slotWidth) + ((slotWidth - iconSize) / 2);
                using var brush = new SolidBrush(foregroundColor);
                FillRoundedRectangle(graphics, brush, new RectangleF(iconLeft, iconTop, iconSize, iconSize), iconSize * 0.22f);

                // "running app" indicator under the first two apps
                if (i < 2)
                {
                    using var indicator = new SolidBrush(indicatorColor);
                    var indicatorWidth = iconSize * 0.3f;
                    FillRoundedRectangle(
                        graphics,
                        indicator,
                        new RectangleF(iconLeft + ((iconSize - indicatorWidth) / 2), taskbar.Bottom - 5, indicatorWidth, 3),
                        1.5f);
                }
            }

            if (showTray)
            {
                using var foreground = new SolidBrush(foregroundColor);
                var clockWidth = TaskbarHeight * 1.2f;
                var clockLeft = width - clockWidth - 14;
                var center = taskbar.Top + (TaskbarHeight / 2f);
                FillRoundedRectangle(graphics, foreground, new RectangleF(clockLeft + (clockWidth * 0.15f), center - 7, clockWidth * 0.85f, 4), 2);
                FillRoundedRectangle(graphics, foreground, new RectangleF(clockLeft, center + 3, clockWidth, 4), 2);

                var glyphSize = 7f;
                for (var i = 0; i < 3; i++)
                {
                    var glyphLeft = clockLeft - 14 - ((3 - i) * (glyphSize + 6));
                    graphics.FillEllipse(foreground, glyphLeft, center - (glyphSize / 2), glyphSize, glyphSize);
                }
            }
        }

        private static void DrawStartLogo(System.Drawing.Graphics graphics, Rectangle bounds)
        {
            using var brush = new LinearGradientBrush(
                bounds,
                Color.FromArgb(255, 76, 194, 255),
                Color.FromArgb(255, 0, 103, 192),
                LinearGradientMode.ForwardDiagonal);
            var gap = Math.Max(1f, bounds.Width / 12f);
            var tile = (bounds.Width - gap) / 2f;
            graphics.FillRectangle(brush, bounds.Left, bounds.Top, tile, tile);
            graphics.FillRectangle(brush, bounds.Left + tile + gap, bounds.Top, tile, tile);
            graphics.FillRectangle(brush, bounds.Left, bounds.Top + tile + gap, tile, tile);
            graphics.FillRectangle(brush, bounds.Left + tile + gap, bounds.Top + tile + gap, tile, tile);
        }

        private static void FillRoundedRectangle(System.Drawing.Graphics graphics, Brush brush, RectangleF bounds, float radius)
        {
            var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            using var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            graphics.FillPath(brush, path);
        }

        private static void DrawImageClamped(System.Drawing.Graphics graphics, Image image, Rectangle destination, RectangleF source)
        {
            // TileFlipXY stops the interpolation from blending transparent pixels in at the edges
            using var attributes = new ImageAttributes();
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            graphics.DrawImage(image, destination, source.X, source.Y, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
        }

        private static Bitmap LoadWallpaper()
        {
            string wallpaperPath = null;
            try
            {
                var builder = new StringBuilder(260);
                if (NativeMethods.SystemParametersInfo(NativeMethods.SPI_GETDESKWALLPAPER, builder.Capacity, builder, 0))
                {
                    wallpaperPath = builder.ToString();
                }
            }
            catch (Exception)
            {
            }

            // no wallpaper path means the user has a solid color background
            if (string.IsNullOrEmpty(wallpaperPath))
            {
                return CreateSolidImage(GetDesktopColor());
            }

            return TryLoadImage(wallpaperPath)
                ?? TryLoadImage(Path.Combine(AppContext.BaseDirectory, "Assets", "Settings", "Modules", "Wallpaper.png"))
                ?? CreateSolidImage(GetDesktopColor());
        }

        private static Bitmap TryLoadImage(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                using var stream = File.OpenRead(path);
                using var source = System.Drawing.Image.FromStream(stream);

                // downscale large wallpapers so we don't keep a huge bitmap in memory
                var scale = Math.Min(1f, 1920f / Math.Max(source.Width, source.Height));
                var result = new Bitmap(
                    Math.Max(1, (int)(source.Width * scale)),
                    Math.Max(1, (int)(source.Height * scale)),
                    PixelFormat.Format32bppPArgb);
                using var graphics = System.Drawing.Graphics.FromImage(result);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                DrawImageClamped(graphics, source, new Rectangle(0, 0, result.Width, result.Height), new RectangleF(0, 0, source.Width, source.Height));
                return result;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Bitmap CreateSolidImage(Color color)
        {
            var image = new Bitmap(16, 9, PixelFormat.Format32bppPArgb);
            using var graphics = System.Drawing.Graphics.FromImage(image);
            graphics.Clear(color);
            return image;
        }

        private static Color GetDesktopColor()
        {
            var colorRef = NativeMethods.GetSysColor(NativeMethods.COLOR_DESKTOP);
            return Color.FromArgb(255, (int)(colorRef & 0xFF), (int)((colorRef >> 8) & 0xFF), (int)((colorRef >> 16) & 0xFF));
        }

        private static bool IsSystemLightTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("SystemUsesLightTheme") is int value ? value != 0 : true;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}
