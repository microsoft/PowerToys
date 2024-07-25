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
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(CanBeSaved)));
            }
        }

        public long CreationTime { get; set; } // in seconds

        public long LastLaunchedTime { get; set; } // in seconds

        public bool IsShortcutNeeded { get; set; }

        public bool MoveExistingWindows { get; set; }

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

        public bool CanBeSaved
        {
            get => Name.Length > 0 && Applications.Count > 0;
        }

        private bool _isRevertEnabled;

        public bool IsRevertEnabled
        {
            get => _isRevertEnabled;
            set
            {
                if (_isRevertEnabled != value)
                {
                    _isRevertEnabled = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsRevertEnabled)));
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
                    MonitorHeaderRow headerRow = new MonitorHeaderRow { MonitorName = appItem.Key.MonitorInfo, SelectString = Properties.Resources.SelectAllAppsOnMonitor + " " + appItem.Key.MonitorInfo };
                    applicationsListed.Add(headerRow);
                    foreach (Application app in appItem)
                    {
                        applicationsListed.Add(app);
                    }
                }

                var minimizedApps = Applications.Where(x => x.Minimized);
                if (minimizedApps.Any())
                {
                    MonitorHeaderRow headerRow = new MonitorHeaderRow { MonitorName = Properties.Resources.Minimized_Apps, SelectString = Properties.Resources.SelectAllMinimizedApps };
                    applicationsListed.Add(headerRow);
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
                int count = Applications.Count;
                return count.ToString(CultureInfo.InvariantCulture) + " " + (count == 1 ? Properties.Resources.App : Properties.Resources.Apps);
            }
        }

        public List<MonitorSetup> Monitors { get; set; }

        private BitmapImage _previewIcons;
        private BitmapImage _previewImage;
        private double _previewImageWidth;

        public Project(Project selectedProject)
        {
            Name = selectedProject.Name;
            PreviewIcons = selectedProject.PreviewIcons;
            PreviewImage = selectedProject.PreviewImage;
            IsShortcutNeeded = selectedProject.IsShortcutNeeded;
            MoveExistingWindows = selectedProject.MoveExistingWindows;

            int screenIndex = 1;

            Monitors = new List<MonitorSetup>();
            foreach (var item in selectedProject.Monitors.OrderBy(x => x.MonitorDpiAwareBounds.Left).ThenBy(x => x.MonitorDpiAwareBounds.Top))
            {
                Monitors.Add(new MonitorSetup($"Screen {screenIndex}", item.MonitorInstanceId, item.MonitorNumber, item.Dpi, item.MonitorDpiAwareBounds, item.MonitorDpiUnawareBounds));
                screenIndex++;
            }

            Applications = new List<Application>();
            foreach (var item in selectedProject.Applications)
            {
                Application newApp = new Application()
                {
                    AppName = item.AppName,
                    AppPath = item.AppPath,
                    AppTitle = item.AppTitle,
                    CommandLineArguments = item.CommandLineArguments,
                    PackageFullName = item.PackageFullName,
                    IsElevated = item.IsElevated,
                    Minimized = item.Minimized,
                    Maximized = item.Maximized,
                    MonitorNumber = item.MonitorNumber,
                    IsNotFound = item.IsNotFound,
                    Position = new Application.WindowPosition() { X = item.Position.X, Y = item.Position.Y, Height = item.Position.Height, Width = item.Position.Width },
                    Parent = this,
                };
                newApp.InitializationFinished();
                Applications.Add(newApp);
            }
        }

        public Project()
        {
        }

        public BitmapImage PreviewIcons
        {
            get
            {
                return _previewIcons;
            }

            set
            {
                _previewIcons = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(PreviewIcons)));
            }
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

        public double PreviewImageWidth
        {
            get
            {
                return _previewImageWidth;
            }

            set
            {
                _previewImageWidth = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(PreviewImageWidth)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        public async void Initialize(Theme currentTheme)
        {
            PreviewIcons = await Task.Run(() => DrawHelper.DrawPreviewIcons(this));
            Rectangle commonBounds = GetCommonBounds();
            PreviewImage = await Task.Run(() => DrawHelper.DrawPreview(this, commonBounds, currentTheme));
            PreviewImageWidth = commonBounds.Width / (commonBounds.Height * 1.2 / 200);
        }

        private Rectangle GetCommonBounds()
        {
            double minX = Monitors.First().MonitorDpiAwareBounds.Left;
            double minY = Monitors.First().MonitorDpiAwareBounds.Top;
            double maxX = Monitors.First().MonitorDpiAwareBounds.Right;
            double maxY = Monitors.First().MonitorDpiAwareBounds.Bottom;
            for (int monitorIndex = 1; monitorIndex < Monitors.Count; monitorIndex++)
            {
                Monitor monitor = Monitors[monitorIndex];
                minX = Math.Min(minX, monitor.MonitorDpiAwareBounds.Left);
                minY = Math.Min(minY, monitor.MonitorDpiAwareBounds.Top);
                maxX = Math.Max(maxX, monitor.MonitorDpiAwareBounds.Right);
                maxY = Math.Max(maxY, monitor.MonitorDpiAwareBounds.Bottom);
            }

            return new Rectangle((int)minX, (int)minY, (int)(maxX - minX), (int)(maxY - minY));
        }

        internal void CloseExpanders()
        {
            foreach (Application app in Applications)
            {
                app.IsExpanded = false;
            }
        }
    }
}
