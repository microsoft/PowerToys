// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

using ManagedCommon;
using WorkspacesEditor.Models;

namespace WorkspacesEditor.Utils
{
    public class DrawHelper
    {
        private static readonly Font Font = new("Tahoma", 24);
        private static readonly double Scale = 0.1;
        private static double gapWidth;
        private static double gapHeight;

        public static BitmapImage DrawPreview(Project project, Rectangle bounds, Theme currentTheme)
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
                    Project project = app.Parent;
                    MonitorSetup monitor = project.GetMonitorForApp(app);
                    if (monitor == null)
                    {
                        // unrealistic case, there are no monitors at all in the workspace, use original rect
                        return new Rectangle(TransformX(app.ScaledPosition.X), TransformY(app.ScaledPosition.Y), Scaled(app.ScaledPosition.Width), Scaled(app.ScaledPosition.Height));
                    }

                    return new Rectangle(TransformX(monitor.MonitorDpiAwareBounds.Left), TransformY(monitor.MonitorDpiAwareBounds.Top), Scaled(monitor.MonitorDpiAwareBounds.Width), Scaled(monitor.MonitorDpiAwareBounds.Height));
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

            // now that all repeat index values are set, update the repeat index strings on UI
            foreach (Application app in project.Applications)
            {
                app.OnPropertyChanged(new PropertyChangedEventArgs("RepeatIndexString"));
            }

            foreach (MonitorSetup monitor in project.Monitors)
            {
                // check for vertical gap
                if (monitor.MonitorDpiAwareBounds.Left > bounds.Left && project.Monitors.Any(x => x.MonitorDpiAwareBounds.Right <= monitor.MonitorDpiAwareBounds.Left))
                {
                    verticalGaps.Add(monitor.MonitorDpiAwareBounds.Left);
                }

                // check for horizontal gap
                if (monitor.MonitorDpiAwareBounds.Top > bounds.Top && project.Monitors.Any(x => x.MonitorDpiAwareBounds.Bottom <= monitor.MonitorDpiAwareBounds.Top))
                {
                    horizontalGaps.Add(monitor.MonitorDpiAwareBounds.Top);
                }
            }

            Bitmap previewBitmap = new(Scaled(bounds.Width + (verticalGaps.Count * gapWidth)), Scaled((bounds.Height * 1.2) + (horizontalGaps.Count * gapHeight)));
            double desiredIconSize = Scaled(Math.Min(bounds.Width, bounds.Height)) * 0.25;
            using (Graphics g = Graphics.FromImage(previewBitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                Brush brush = new SolidBrush(currentTheme == Theme.Dark ? Color.FromArgb(10, 255, 255, 255) : Color.FromArgb(10, 0, 0, 0));
                Brush brushForHighlight = new SolidBrush(currentTheme == Theme.Dark ? Color.FromArgb(192, 255, 255, 255) : Color.FromArgb(192, 0, 0, 0));

                // draw the monitors
                foreach (MonitorSetup monitor in project.Monitors)
                {
                    Brush monitorBrush = new SolidBrush(currentTheme == Theme.Dark ? Color.FromArgb(32, 7, 91, 155) : Color.FromArgb(32, 7, 91, 155));
                    g.FillRectangle(monitorBrush, new Rectangle(TransformX(monitor.MonitorDpiAwareBounds.Left), TransformY(monitor.MonitorDpiAwareBounds.Top), Scaled(monitor.MonitorDpiAwareBounds.Width), Scaled(monitor.MonitorDpiAwareBounds.Height)));
                }

                IEnumerable<Application> appsToDraw = appsIncluded.Where(x => !x.Minimized);

                // draw the highlighted app at the end to have its icon in the foreground for the case there are overlapping icons
                foreach (Application app in appsToDraw.Where(x => !x.IsHighlighted))
                {
                    Rectangle rect = GetAppRect(app);
                    DrawWindow(g, brush, rect, app, desiredIconSize, currentTheme);
                }

                foreach (Application app in appsToDraw.Where(x => x.IsHighlighted))
                {
                    Rectangle rect = GetAppRect(app);
                    DrawWindow(g, brushForHighlight, rect, app, desiredIconSize, currentTheme);
                }

                // draw the minimized windows
                Rectangle rectMinimized = new(0, Scaled((bounds.Height * 1.02) + (horizontalGaps.Count * gapHeight)), Scaled(bounds.Width + (verticalGaps.Count * gapWidth)), Scaled(bounds.Height * 0.18));
                DrawWindow(g, brush, brushForHighlight, rectMinimized, appsIncluded.Where(x => x.Minimized), currentTheme);
            }

            using MemoryStream memory = new();
            WorkspacesCsharpLibrary.DrawHelper.SaveBitmap(previewBitmap, memory);

            memory.Position = 0;

            BitmapImage bitmapImage = new();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            return bitmapImage;
        }

        public static void DrawWindow(Graphics graphics, Brush brush, Rectangle bounds, Application app, double desiredIconSize, Theme currentTheme)
        {
            if (graphics == null)
            {
                return;
            }

            if (brush == null)
            {
                return;
            }

            using (GraphicsPath path = RoundedRect(bounds))
            {
                if (app.IsHighlighted)
                {
                    graphics.DrawPath(new Pen(currentTheme == Theme.Dark ? Color.White : Color.DarkGray, graphics.VisibleClipBounds.Height / 50), path);
                }
                else
                {
                    graphics.DrawPath(new Pen(currentTheme == Theme.Dark ? Color.FromArgb(128, 82, 82, 82) : Color.FromArgb(128, 160, 160, 160), graphics.VisibleClipBounds.Height / 200), path);
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

                    SizeF textSize = graphics.MeasureString(indexString, Font);
                    GraphicsState state = graphics.Save();
                    graphics.TranslateTransform(indexBounds.Left, indexBounds.Top);
                    graphics.ScaleTransform(indexBounds.Width / textSize.Width, indexBounds.Height / textSize.Height);
                    graphics.DrawString(indexString, Font, Brushes.Black, PointF.Empty);
                    graphics.Restore(state);
                }
            }
            catch (Exception)
            {
                // sometimes drawing an icon throws an exception despite that the icon seems to be ok
            }
        }

        public static void DrawWindow(Graphics graphics, Brush brush, Brush brushForHighlight, Rectangle bounds, IEnumerable<Application> apps, Theme currentTheme)
        {
            int appsCount = apps.Count();
            if (appsCount == 0)
            {
                return;
            }

            if (graphics == null)
            {
                return;
            }

            if (brush == null)
            {
                return;
            }

            using (GraphicsPath path = RoundedRect(bounds))
            {
                if (apps.Where(x => x.IsHighlighted).Any())
                {
                    graphics.DrawPath(new Pen(currentTheme == Theme.Dark ? Color.White : Color.DarkGray, graphics.VisibleClipBounds.Height / 50), path);
                    graphics.FillPath(brushForHighlight, path);
                }
                else
                {
                    graphics.DrawPath(new Pen(currentTheme == Theme.Dark ? Color.FromArgb(128, 82, 82, 82) : Color.FromArgb(128, 160, 160, 160), graphics.VisibleClipBounds.Height / 200), path);
                    graphics.FillPath(brush, path);
                }
            }

            double iconSize = Math.Min(bounds.Width, bounds.Height) * 0.5;
            for (int iconCounter = 0; iconCounter < appsCount; iconCounter++)
            {
                Application app = apps.ElementAt(iconCounter);
                Rectangle iconBounds = new((int)(bounds.Left + (bounds.Width / 2) - (iconSize * ((appsCount / 2) - iconCounter))), (int)(bounds.Top + (bounds.Height / 2) - (iconSize / 2)), (int)iconSize, (int)iconSize);

                try
                {
                    graphics.DrawIcon(app.Icon, iconBounds);
                    if (app.RepeatIndex > 0)
                    {
                        string indexString = app.RepeatIndex.ToString(CultureInfo.InvariantCulture);
                        int indexSize = (int)(iconBounds.Width * 0.5);
                        Rectangle indexBounds = new(iconBounds.Right - indexSize, iconBounds.Bottom - indexSize, indexSize, indexSize);

                        SizeF textSize = graphics.MeasureString(indexString, Font);
                        GraphicsState state = graphics.Save();
                        graphics.TranslateTransform(indexBounds.Left, indexBounds.Top);
                        graphics.ScaleTransform(indexBounds.Width / textSize.Width, indexBounds.Height / textSize.Height);
                        graphics.DrawString(indexString, Font, Brushes.Black, PointF.Empty);
                        graphics.Restore(state);
                    }
                }
                catch (Exception)
                {
                    // sometimes drawing an icon throws an exception despite that the icon seems to be ok
                }
            }
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
                        graphics.DrawIcon(app.Icon, new Rectangle(32 * appIndex, 0, 24, 24));
                    }
                    catch (Exception e)
                    {
                        Logger.LogError($"Exception while drawing the icon for app {app.AppName}. Exception message: {e.Message}");
                    }

                    appIndex++;
                }
            }

            using MemoryStream memory = new();

            WorkspacesCsharpLibrary.DrawHelper.SaveBitmap(previewBitmap, memory);

            memory.Position = 0;

            BitmapImage bitmapImage = new();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            return bitmapImage;
        }

        private static GraphicsPath RoundedRect(Rectangle bounds)
        {
            int minorSize = Math.Min(bounds.Width, bounds.Height);
            int radius = (int)(minorSize / 8);

            int diameter = radius * 2;
            Size size = new(diameter, diameter);
            Rectangle arc = new(bounds.Location, size);
            GraphicsPath path = new();

            if (radius == 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            // top left arc
            path.AddArc(arc, 180, 90);

            // top right arc
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);

            // bottom right arc
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // bottom left arc
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
    }
}
