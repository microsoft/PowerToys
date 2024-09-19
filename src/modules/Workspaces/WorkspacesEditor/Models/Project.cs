// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

using ManagedCommon;
using WorkspacesEditor.Data;
using WorkspacesEditor.Utils;

namespace WorkspacesEditor.Models
{
    public class Project : INotifyPropertyChanged
    {
        [JsonIgnore]
        public string EditorWindowTitle { get; set; }

        public string Id { get; private set; }

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

        public long CreationTime { get; } // in seconds

        public long LastLaunchedTime { get; } // in seconds

        public bool IsShortcutNeeded { get; set; }

        public bool MoveExistingWindows { get; set; }

        public string LastLaunched
        {
            get
            {
                string lastLaunched = WorkspacesEditor.Properties.Resources.LastLaunched + ": ";
                if (LastLaunchedTime == 0)
                {
                    return lastLaunched + WorkspacesEditor.Properties.Resources.Never;
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
                    return lastLaunched + WorkspacesEditor.Properties.Resources.Recently;
                }

                if (delta < 2 * MINUTE)
                {
                    return lastLaunched + WorkspacesEditor.Properties.Resources.OneMinuteAgo;
                }

                if (delta < 45 * MINUTE)
                {
                    return lastLaunched + ts.Minutes + " " + WorkspacesEditor.Properties.Resources.MinutesAgo;
                }

                if (delta < 90 * MINUTE)
                {
                    return lastLaunched + WorkspacesEditor.Properties.Resources.OneHourAgo;
                }

                if (delta < 24 * HOUR)
                {
                    return lastLaunched + ts.Hours + " " + WorkspacesEditor.Properties.Resources.HoursAgo;
                }

                if (delta < 48 * HOUR)
                {
                    return lastLaunched + WorkspacesEditor.Properties.Resources.Yesterday;
                }

                if (delta < 30 * DAY)
                {
                    return lastLaunched + ts.Days + " " + WorkspacesEditor.Properties.Resources.DaysAgo;
                }

                if (delta < 12 * MONTH)
                {
                    int months = Convert.ToInt32(Math.Floor((double)ts.Days / 30));
                    return lastLaunched + (months <= 1 ? WorkspacesEditor.Properties.Resources.OneMonthAgo : months + " " + WorkspacesEditor.Properties.Resources.MonthsAgo);
                }
                else
                {
                    int years = Convert.ToInt32(Math.Floor((double)ts.Days / 365));
                    return lastLaunched + (years <= 1 ? WorkspacesEditor.Properties.Resources.OneYearAgo : years + " " + WorkspacesEditor.Properties.Resources.YearsAgo);
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
                    MonitorHeaderRow headerRow = new MonitorHeaderRow { MonitorName = "Screen " + appItem.Key.MonitorNumber, SelectString = Properties.Resources.SelectAllAppsOnMonitor + " " + appItem.Key.MonitorInfo };
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

        public List<MonitorSetup> Monitors { get; }

        public bool IsPositionChangedManually { get; set; } // telemetry

        private BitmapImage _previewIcons;
        private BitmapImage _previewImage;
        private double _previewImageWidth;

        public Project(Project selectedProject)
        {
            Id = selectedProject.Id;
            Name = selectedProject.Name;
            PreviewIcons = selectedProject.PreviewIcons;
            PreviewImage = selectedProject.PreviewImage;
            IsShortcutNeeded = selectedProject.IsShortcutNeeded;
            MoveExistingWindows = selectedProject.MoveExistingWindows;

            int screenIndex = 1;

            Monitors = new List<MonitorSetup>();
            foreach (var item in selectedProject.Monitors.OrderBy(x => x.MonitorDpiAwareBounds.Left).ThenBy(x => x.MonitorDpiAwareBounds.Top))
            {
                Monitors.Add(item);
                screenIndex++;
            }

            Applications = new List<Application>();
            foreach (var item in selectedProject.Applications)
            {
                Application newApp = new Application(item);
                newApp.Parent = this;
                newApp.InitializationFinished();
                Applications.Add(newApp);
            }
        }

        public Project(ProjectData.ProjectWrapper project)
        {
            Id = project.Id;
            Name = project.Name;
            CreationTime = project.CreationTime;
            LastLaunchedTime = project.LastLaunchedTime;
            IsShortcutNeeded = project.IsShortcutNeeded;
            MoveExistingWindows = project.MoveExistingWindows;
            Monitors = new List<MonitorSetup>() { };
            Applications = new List<Models.Application> { };

            foreach (var app in project.Applications)
            {
                Models.Application newApp = new Models.Application()
                {
                    AppName = app.Application,
                    AppPath = app.ApplicationPath,
                    AppTitle = app.Title,
                    PackageFullName = app.PackageFullName,
                    AppUserModelId = app.AppUserModelId,
                    Parent = this,
                    CommandLineArguments = app.CommandLineArguments,
                    IsElevated = app.IsElevated,
                    CanLaunchElevated = app.CanLaunchElevated,
                    Maximized = app.Maximized,
                    Minimized = app.Minimized,
                    IsNotFound = false,
                    Position = new Models.Application.WindowPosition()
                    {
                        Height = app.Position.Height,
                        Width = app.Position.Width,
                        X = app.Position.X,
                        Y = app.Position.Y,
                    },
                    MonitorNumber = app.Monitor,
                };
                newApp.InitializationFinished();
                Applications.Add(newApp);
            }

            foreach (var monitor in project.MonitorConfiguration)
            {
                System.Windows.Rect dpiAware = new System.Windows.Rect(monitor.MonitorRectDpiAware.Left, monitor.MonitorRectDpiAware.Top, monitor.MonitorRectDpiAware.Width, monitor.MonitorRectDpiAware.Height);
                System.Windows.Rect dpiUnaware = new System.Windows.Rect(monitor.MonitorRectDpiUnaware.Left, monitor.MonitorRectDpiUnaware.Top, monitor.MonitorRectDpiUnaware.Width, monitor.MonitorRectDpiUnaware.Height);
                Monitors.Add(new MonitorSetup(monitor.Id, monitor.InstanceId, monitor.MonitorNumber, monitor.Dpi, dpiAware, dpiUnaware));
            }
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

        public void UpdateAfterLaunchAndEdit(Project other)
        {
            Id = other.Id;
            Name = other.Name;
            IsRevertEnabled = true;
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
