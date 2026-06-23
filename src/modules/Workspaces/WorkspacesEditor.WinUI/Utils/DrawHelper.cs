// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;

using Microsoft.UI.Xaml.Media.Imaging;

using WorkspacesEditor.Models;

namespace WorkspacesEditor.Utils
{
    internal static class DrawHelper
    {
        private static readonly Font DrawFont = new("Tahoma", 24);
        private static readonly double Scale = 0.1;
        private static double gapWidth;
        private static double gapHeight;

        public static BitmapImage DrawPreview(Project project, Rectangle bounds, bool isDarkTheme)
        {
            List<double> horizontalGaps = [];
            List<double> verticalGaps = [];
            gapWidth = bounds.Width * 0.01;
            gapHeight = bounds.Height * 0.01;

            int Scaled(double value)
            {
                return (int)(value * Scale);
            }

            int TransformX(double posX)
            {
                double gapTransform = verticalGaps.Where(x => x <= posX).Count() * gapWidth;
                return Scaled(posX - bounds.Left + gapTransform);
            }

            int TransformY(double posY)
            {
                double gapTransform = horizontalGaps.Where(x => x <= posY).Count() * gapHeight;
                return Scaled(posY - bounds.Top + gapTransform);
            }

            Rectangle GetAppRect(Application app)
            {
                if (app.Maximized)
                {
                    MonitorSetup monitor = project.GetMonitorForApp(app);
                    if (monitor == null)
                    {
                        return new Rectangle(TransformX(app.ScaledPosition.X), TransformY(app.ScaledPosition.Y), Scaled(app.ScaledPosition.Width), Scaled(app.ScaledPosition.Height));
                    }

                    return new Rectangle(TransformX(monitor.MonitorDpiAwareBounds.X), TransformY(monitor.MonitorDpiAwareBounds.Y), Scaled(monitor.MonitorDpiAwareBounds.Width), Scaled(monitor.MonitorDpiAwareBounds.Height));
                }
                else
                {
                    return new Rectangle(TransformX(app.ScaledPosition.X), TransformY(app.ScaledPosition.Y), Scaled(app.ScaledPosition.Width), Scaled(app.ScaledPosition.Height));
                }
            }

            Dictionary<string, int> repeatCounter = [];

            IEnumerable<Application> appsIncluded = project.Applications.Where(x => x.IsIncluded);

            foreach (Application app in appsIncluded)
            {
                string appIdentifier = app.AppPath + app.PwaAppId;
                if (repeatCounter.TryGetValue(appIdentifier, out int value))
                {
                    repeatCounter[appIdentifier] = ++value;
                }
                else
                {
                    repeatCounter.Add(appIdentifier, 1);
                }

                app.RepeatIndex = repeatCounter[appIdentifier];
            }

            foreach (Application app in project.Applications.Where(x => !x.IsIncluded))
            {
                app.RepeatIndex = 0;
            }

            foreach (Application app in project.Applications)
            {
                app.OnPropertyChanged(new PropertyChangedEventArgs("RepeatIndexString"));
            }

            foreach (MonitorSetup monitor in project.Monitors)
            {
                if (monitor.MonitorDpiAwareBounds.X > bounds.Left && project.Monitors.Any(x => (x.MonitorDpiAwareBounds.X + x.MonitorDpiAwareBounds.Width) <= monitor.MonitorDpiAwareBounds.X))
                {
                    verticalGaps.Add(monitor.MonitorDpiAwareBounds.X);
                }

                if (monitor.MonitorDpiAwareBounds.Y > bounds.Top && project.Monitors.Any(x => (x.MonitorDpiAwareBounds.Y + x.MonitorDpiAwareBounds.Height) <= monitor.MonitorDpiAwareBounds.Y))
                {
                    horizontalGaps.Add(monitor.MonitorDpiAwareBounds.Y);
                }
            }

            Bitmap previewBitmap = new(Scaled(bounds.Width + (verticalGaps.Count * gapWidth)), Scaled((bounds.Height * 1.2) + (horizontalGaps.Count * gapHeight)));
            double desiredIconSize = Scaled(Math.Min(bounds.Width, bounds.Height)) * 0.25;
            using (Graphics g = Graphics.FromImage(previewBitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                Brush brush = new SolidBrush(isDarkTheme ? Color.FromArgb(10, 255, 255, 255) : Color.FromArgb(10, 0, 0, 0));
                Brush brushForHighlight = new SolidBrush(isDarkTheme ? Color.FromArgb(192, 255, 255, 255) : Color.FromArgb(192, 0, 0, 0));

                foreach (MonitorSetup monitor in project.Monitors)
                {
                    Brush monitorBrush = new SolidBrush(Color.FromArgb(32, 7, 91, 155));
                    g.FillRectangle(monitorBrush, new Rectangle(TransformX(monitor.MonitorDpiAwareBounds.X), TransformY(monitor.MonitorDpiAwareBounds.Y), Scaled(monitor.MonitorDpiAwareBounds.Width), Scaled(monitor.MonitorDpiAwareBounds.Height)));
                }

                IEnumerable<Application> appsToDraw = appsIncluded.Where(x => !x.Minimized);

                foreach (Application app in appsToDraw.Where(x => !x.IsHighlighted))
                {
                    Rectangle rect = GetAppRect(app);
                    DrawWindow(g, brush, rect, app, desiredIconSize, isDarkTheme);
                }

                foreach (Application app in appsToDraw.Where(x => x.IsHighlighted))
                {
                    Rectangle rect = GetAppRect(app);
                    DrawWindow(g, brushForHighlight, rect, app, desiredIconSize, isDarkTheme);
                }

                Rectangle rectMinimized = new(0, Scaled((bounds.Height * 1.02) + (horizontalGaps.Count * gapHeight)), Scaled(bounds.Width + (verticalGaps.Count * gapWidth)), Scaled(bounds.Height * 0.18));
                DrawWindow(g, brush, brushForHighlight, rectMinimized, appsIncluded.Where(x => x.Minimized), isDarkTheme);
            }

            return BitmapToWinUiImage(previewBitmap);
        }

        public static BitmapImage DrawPreviewIcons(Project project)
        {
            int appsCount = project.Applications.Count;
            if (appsCount == 0)
            {
                return null;
            }

            Bitmap previewBitmap = new(32 * appsCount, 24);
            using (Graphics graphics = Graphics.FromImage(previewBitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                int appIndex = 0;
                foreach (Application app in project.Applications)
                {
                    try
                    {
                        graphics.DrawIcon(app.Icon, new Rectangle(appIndex * 32, 0, 24, 24));
                    }
                    catch (Exception)
                    {
                    }

                    appIndex++;
                }
            }

            return BitmapToWinUiImage(previewBitmap);
        }

        private static void DrawWindow(Graphics graphics, Brush brush, Rectangle bounds, Application app, double desiredIconSize, bool isDarkTheme)
        {
            if (graphics == null || brush == null)
            {
                return;
            }

            using (GraphicsPath path = RoundedRect(bounds))
            {
                if (app.IsHighlighted)
                {
                    graphics.DrawPath(new Pen(isDarkTheme ? Color.White : Color.DarkGray, graphics.VisibleClipBounds.Height / 50), path);
                }
                else
                {
                    graphics.DrawPath(new Pen(isDarkTheme ? Color.FromArgb(128, 82, 82, 82) : Color.FromArgb(128, 160, 160, 160), graphics.VisibleClipBounds.Height / 200), path);
                }

                graphics.FillPath(brush, path);
            }

            double iconSize = Math.Min(Math.Min(bounds.Width - 4, bounds.Height - 4), desiredIconSize);
            Rectangle iconBounds = new((int)(bounds.Left + (bounds.Width / 2) - (iconSize / 2)), (int)(bounds.Top + (bounds.Height / 2) - (iconSize / 2)), (int)iconSize, (int)iconSize);

            try
            {
                graphics.DrawIcon(app.Icon, iconBounds);
                if (app.RepeatIndex > 1)
                {
                    string indexString = app.RepeatIndex.ToString(CultureInfo.InvariantCulture);
                    int indexSize = (int)(iconBounds.Width * 0.5);
                    Rectangle indexBounds = new(iconBounds.Right - indexSize, iconBounds.Bottom - indexSize, indexSize, indexSize);

                    SizeF textSize = graphics.MeasureString(indexString, DrawFont);
                    GraphicsState state = graphics.Save();
                    graphics.TranslateTransform(indexBounds.Left, indexBounds.Top);
                    graphics.ScaleTransform(indexBounds.Width / textSize.Width, indexBounds.Height / textSize.Height);
                    graphics.DrawString(indexString, DrawFont, Brushes.Black, PointF.Empty);
                    graphics.Restore(state);
                }
            }
            catch (Exception)
            {
            }
        }

        private static void DrawWindow(Graphics graphics, Brush brush, Brush brushForHighlight, Rectangle bounds, IEnumerable<Application> apps, bool isDarkTheme)
        {
            int appsCount = apps.Count();
            if (appsCount == 0 || graphics == null || brush == null)
            {
                return;
            }

            using (GraphicsPath path = RoundedRect(bounds))
            {
                if (apps.Any(x => x.IsHighlighted))
                {
                    graphics.DrawPath(new Pen(isDarkTheme ? Color.White : Color.DarkGray, graphics.VisibleClipBounds.Height / 50), path);
                    graphics.FillPath(brushForHighlight, path);
                }
                else
                {
                    graphics.DrawPath(new Pen(isDarkTheme ? Color.FromArgb(128, 82, 82, 82) : Color.FromArgb(128, 160, 160, 160), graphics.VisibleClipBounds.Height / 200), path);
                    graphics.FillPath(brush, path);
                }
            }

            double iconSize = Math.Min(bounds.Width, bounds.Height) * 0.5;
            for (int iconCounter = 0; iconCounter < appsCount; iconCounter++)
            {
                Application app = apps.ElementAt(iconCounter);
                Rectangle iconBounds = new((int)(bounds.Left + (bounds.Width / 2) - (iconSize * ((appsCount / 2.0) - iconCounter))), (int)(bounds.Top + (bounds.Height / 2) - (iconSize / 2)), (int)iconSize, (int)iconSize);

                try
                {
                    graphics.DrawIcon(app.Icon, iconBounds);
                    if (app.RepeatIndex > 0)
                    {
                        string indexString = app.RepeatIndex.ToString(CultureInfo.InvariantCulture);
                        int indexSize = (int)(iconBounds.Width * 0.5);
                        Rectangle indexBounds = new(iconBounds.Right - indexSize, iconBounds.Bottom - indexSize, indexSize, indexSize);

                        SizeF textSize = graphics.MeasureString(indexString, DrawFont);
                        GraphicsState state = graphics.Save();
                        graphics.TranslateTransform(indexBounds.Left, indexBounds.Top);
                        graphics.ScaleTransform(indexBounds.Width / textSize.Width, indexBounds.Height / textSize.Height);
                        graphics.DrawString(indexString, DrawFont, Brushes.Black, PointF.Empty);
                        graphics.Restore(state);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private static BitmapImage BitmapToWinUiImage(Bitmap bitmap)
        {
            using MemoryStream memory = new();
            WorkspacesCsharpLibrary.DrawHelper.SaveBitmap(bitmap, memory);
            memory.Position = 0;

            BitmapImage bitmapImage = new();
            bitmapImage.SetSource(memory.AsRandomAccessStream());
            return bitmapImage;
        }

        private static GraphicsPath RoundedRect(Rectangle bounds)
        {
            int minorSize = Math.Min(bounds.Width, bounds.Height);
            int radius = minorSize / 8;

            int diameter = radius * 2;
            Size size = new(diameter, diameter);
            Rectangle arc = new(bounds.Location, size);
            GraphicsPath path = new();

            if (radius == 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
