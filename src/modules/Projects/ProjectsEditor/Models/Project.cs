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
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using ManagedCommon;
using ProjectsEditor.Utils;

namespace ProjectsEditor.Models
{
    public class Project : INotifyPropertyChanged
    {
        public class ScreenHeader : Application
        {
            public string Title { get; set; }
        }

        [JsonIgnore]
        public string EditorWindowTitle { get; set; }

        public string Id { get; set; }

        private string _name;

        public string Name
        {
            get
            {
                return _name;
            }

            set
            {
                _name = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public long CreationTime { get; set; } // in seconds

        public long LastLaunchedTime { get; set; } // in seconds

        public bool IsShortcutNeeded { get; set; }

        public string LastLaunched
        {
            get
            {
                string lastLaunched = ProjectsEditor.Properties.Resources.LastLaunched + ": ";
                if (LastLaunchedTime == 0)
                {
                    return lastLaunched + ProjectsEditor.Properties.Resources.Never;
                }

                const int SECOND = 1;
                const int MINUTE = 60 * SECOND;
                const int HOUR = 60 * MINUTE;
                const int DAY = 24 * HOUR;
                const int MONTH = 30 * DAY;

                DateTime lastLaunchDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(LastLaunchedTime);

                var now = DateTime.UtcNow.Ticks;
                var ts = DateTime.UtcNow - lastLaunchDateTime;
                double delta = Math.Abs(ts.TotalSeconds);

                if (delta < 1 * MINUTE)
                {
                    return lastLaunched + ProjectsEditor.Properties.Resources.Recently;
                }

                if (delta < 2 * MINUTE)
                {
                    return lastLaunched + ProjectsEditor.Properties.Resources.OneMinuteAgo;
                }

                if (delta < 45 * MINUTE)
                {
                    return lastLaunched + ts.Minutes + " " + ProjectsEditor.Properties.Resources.MinutesAgo;
                }

                if (delta < 90 * MINUTE)
                {
                    return lastLaunched + ProjectsEditor.Properties.Resources.OneHourAgo;
                }

                if (delta < 24 * HOUR)
                {
                    return lastLaunched + ts.Hours + " " + ProjectsEditor.Properties.Resources.HoursAgo;
                }

                if (delta < 48 * HOUR)
                {
                    return lastLaunched + ProjectsEditor.Properties.Resources.Yesterday;
                }

                if (delta < 30 * DAY)
                {
                    return lastLaunched + ts.Days + " " + ProjectsEditor.Properties.Resources.DaysAgo;
                }

                if (delta < 12 * MONTH)
                {
                    int months = Convert.ToInt32(Math.Floor((double)ts.Days / 30));
                    return lastLaunched + (months <= 1 ? ProjectsEditor.Properties.Resources.OneMonthAgo : months + " " + ProjectsEditor.Properties.Resources.MonthsAgo);
                }
                else
                {
                    int years = Convert.ToInt32(Math.Floor((double)ts.Days / 365));
                    return lastLaunched + (years <= 1 ? ProjectsEditor.Properties.Resources.OneYearAgo : years + " " + ProjectsEditor.Properties.Resources.YearsAgo);
                }
            }
        }

        private bool _isPopupVisible;

        [JsonIgnore]
        public bool IsPopupVisible
        {
            get
            {
                return _isPopupVisible;
            }

            set
            {
                _isPopupVisible = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsPopupVisible)));
            }
        }

        public List<Application> Applications { get; set; }

        public List<object> ApplicationsListed
        {
            get
            {
                List<object> applicationsListed = new List<object>();
                ILookup<MonitorSetup, Application> apps = Applications.Where(x => !x.Minimized).ToLookup(x => x.MonitorSetup);
                foreach (var appItem in apps.OrderBy(x => x.Key.MonitorDpiUnawareBounds.Left).ThenBy(x => x.Key.MonitorDpiUnawareBounds.Top))
                {
                    applicationsListed.Add(appItem.Key.MonitorInfo);
                    foreach (Application app in appItem)
                    {
                        applicationsListed.Add(app);
                    }
                }

                var minimizedApps = Applications.Where(x => x.Minimized);
                if (minimizedApps.Any())
                {
                    applicationsListed.Add("Minimized Apps");
                    foreach (Application app in minimizedApps)
                    {
                        applicationsListed.Add(app);
                    }
                }

                return applicationsListed;
            }
        }

        [JsonIgnore]
        public string AppsCountString
        {
            get
            {
                return Applications.Where(x => x.IsSelected).Count().ToString(CultureInfo.InvariantCulture) + " apps";
            }
        }

        public List<MonitorSetup> Monitors { get; set; }

        private BitmapImage _previewImage;

        public Project(Project selectedProject)
        {
            Name = selectedProject.Name;
            PreviewImage = selectedProject.PreviewImage;
            IsShortcutNeeded = selectedProject.IsShortcutNeeded;

            int screenIndex = 1;

            Monitors = new List<MonitorSetup>();
            foreach (var item in selectedProject.Monitors.OrderBy(x => x.MonitorDpiAwareBounds.Left).ThenBy(x => x.MonitorDpiAwareBounds.Top))
            {
                Monitors.Add(new MonitorSetup($"Screen {screenIndex}", item.MonitorInstanceId, item.MonitorNumber, item.Dpi, item.MonitorDpiAwareBounds, item.MonitorDpiUnawareBounds) { PreviewImage = item.PreviewImage });
                screenIndex++;
            }

            Applications = new List<Application>();
            foreach (var item in selectedProject.Applications)
            {
                Applications.Add(new Application()
                {
                    Hwnd = item.Hwnd,
                    AppPath = item.AppPath,
                    AppTitle = item.AppTitle,
                    CommandLineArguments = item.CommandLineArguments,
                    Minimized = item.Minimized,
                    Maximized = item.Maximized,
                    IsSelected = item.IsSelected,
                    MonitorNumber = item.MonitorNumber,
                    Position = new Application.WindowPosition() { X = item.Position.X, Y = item.Position.Y, Height = item.Position.Height, Width = item.Position.Width },
                    Parent = this,
                });
            }
        }

        public Project()
        {
        }

        public BitmapImage PreviewImage
        {
            get
            {
                return _previewImage;
            }

            set
            {
                _previewImage = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(PreviewImage)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        public async void Initialize()
        {
            PreviewImage = await Task.Run(() => DrawPreviewIcons());
            foreach (MonitorSetup monitor in Monitors)
            {
                System.Windows.Rect rect = monitor.MonitorDpiAwareBounds;
                monitor.PreviewImage = await Task.Run(() => DrawHelper.DrawPreview(this, new Rectangle((int)rect.Left, (int)rect.Top, (int)(rect.Right - rect.Left), (int)(rect.Bottom - rect.Top))));
            }
        }

        private BitmapImage DrawPreviewIcons()
        {
            var selectedApps = Applications.Where(x => x.IsSelected);
            int appsCount = selectedApps.Count();
            if (appsCount == 0)
            {
                return null;
            }

            Bitmap previewBitmap = new Bitmap(32 * appsCount, 24);
            using (Graphics graphics = Graphics.FromImage(previewBitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                int appIndex = 0;
                foreach (var app in selectedApps)
                {
                    try
                    {
                        graphics.DrawIcon(app.Icon, new Rectangle(32 * appIndex, 0, 24, 24));
                    }
                    catch (Exception e)
                    {
                        Logger.LogError($"Exception while drawing the icon for app {Name}. Exception message: {e.Message}");
                    }

                    appIndex++;
                }
            }

            using (var memory = new MemoryStream())
            {
                previewBitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                return bitmapImage;
            }
        }

        private Rectangle GetCommonBounds()
        {
            double minX = Monitors.First().MonitorDpiUnawareBounds.Left;
            double minY = Monitors.First().MonitorDpiUnawareBounds.Top;
            double maxX = Monitors.First().MonitorDpiUnawareBounds.Right;
            double maxY = Monitors.First().MonitorDpiUnawareBounds.Bottom;
            foreach (var monitor in Monitors)
            {
                minX = Math.Min(minX, monitor.MonitorDpiUnawareBounds.Left);
                minY = Math.Min(minY, monitor.MonitorDpiUnawareBounds.Top);
                maxX = Math.Max(maxX, monitor.MonitorDpiUnawareBounds.Right);
                maxY = Math.Max(maxY, monitor.MonitorDpiUnawareBounds.Bottom);
            }

            return new Rectangle((int)minX, (int)minY, (int)(maxX - minX), (int)(maxY - minY));
        }
    }
}
