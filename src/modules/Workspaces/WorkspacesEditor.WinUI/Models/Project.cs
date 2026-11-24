// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;

using CommunityToolkit.Mvvm.ComponentModel;

using Microsoft.UI.Xaml.Media.Imaging;

using Windows.Foundation;
using WorkspacesCsharpLibrary.Data;
using WorkspacesEditor.Helpers;

namespace WorkspacesEditor.Models
{
    public partial class Project : ObservableObject
    {
        [JsonIgnore]
        public string EditorWindowTitle { get; set; }

        public string Id { get; private set; }

        internal bool NeedsApplicationIdRepair { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanBeSaved))]
        public partial string Name { get; set; }

        public long CreationTime { get; }

        public long LastLaunchedTime { get; private set; }

        public bool IsShortcutNeeded { get; set; }

        public bool MoveExistingWindows { get; set; }

        public string LastLaunched
        {
            get
            {
                string lastLaunched = GetString("LastLaunched") + ": ";
                if (LastLaunchedTime == 0)
                {
                    return lastLaunched + GetString("Never");
                }

                const int Second = 1;
                const int Minute = 60 * Second;
                const int Hour = 60 * Minute;
                const int Day = 24 * Hour;
                const int Month = 30 * Day;

                DateTime lastLaunchDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(LastLaunchedTime);

                TimeSpan ts = DateTime.UtcNow - lastLaunchDateTime;
                double delta = Math.Abs(ts.TotalSeconds);

                if (delta < 1 * Minute)
                {
                    return lastLaunched + GetString("Recently");
                }

                if (delta < 2 * Minute)
                {
                    return lastLaunched + GetString("OneMinuteAgo");
                }

                if (delta < 45 * Minute)
                {
                    return lastLaunched + ts.Minutes + " " + GetString("MinutesAgo");
                }

                if (delta < 90 * Minute)
                {
                    return lastLaunched + GetString("OneHourAgo");
                }

                if (delta < 24 * Hour)
                {
                    return lastLaunched + ts.Hours + " " + GetString("HoursAgo");
                }

                if (delta < 48 * Hour)
                {
                    return lastLaunched + GetString("Yesterday");
                }

                if (delta < 30 * Day)
                {
                    return lastLaunched + ts.Days + " " + GetString("DaysAgo");
                }

                if (delta < 12 * Month)
                {
                    int months = Convert.ToInt32(Math.Floor((double)ts.Days / 30));
                    return lastLaunched + (months <= 1 ? GetString("OneMonthAgo") : months + " " + GetString("MonthsAgo"));
                }
                else
                {
                    int years = Convert.ToInt32(Math.Floor((double)ts.Days / 365));
                    return lastLaunched + (years <= 1 ? GetString("OneYearAgo") : years + " " + GetString("YearsAgo"));
                }
            }
        }

        public bool CanBeSaved => !string.IsNullOrEmpty(Name) &&
            Applications?.Exists(app => app.IsIncluded) == true &&
            Applications.TrueForAll(app => !app.IsIncluded || (app.Position.Width > 0 && app.Position.Height > 0)) &&
            !HasUnresolvedMonitors;

        public bool HasUnresolvedMonitors
        {
            get
            {
                var numbers = new HashSet<int>();
                foreach (MonitorSetup monitor in Monitors)
                {
                    if (monitor.Dpi <= 0 || !numbers.Add(monitor.MonitorNumber))
                    {
                        return true;
                    }
                }

                return Applications?.Exists(app => app.IsIncluded && !numbers.Contains(app.MonitorNumber)) == true;
            }
        }

        [ObservableProperty]
        public partial bool IsRevertEnabled { get; set; }

        [ObservableProperty]
        [JsonIgnore]
        public partial bool IsPopupVisible { get; set; }

        public List<Application> Applications { get; set; }

        public List<object> ApplicationsListed
        {
            get
            {
                List<object> applicationsListed = [];
                var apps = Applications.Where(app => !app.Minimized).GroupBy(app => app.MonitorNumber)
                    .Select(group => (Apps: group, Monitor: group.First().MonitorSetup));
                foreach (var (group, monitor) in apps.OrderBy(item => item.Monitor.MonitorDpiUnawareBounds.X).ThenBy(item => item.Monitor.MonitorDpiUnawareBounds.Y))
                {
                    MonitorHeaderRow headerRow = new() { MonitorName = GetString("Screen") + " " + monitor.MonitorNumber, SelectString = GetString("SelectAllAppsOnMonitor") + " " + monitor.MonitorInfo };
                    applicationsListed.Add(headerRow);
                    applicationsListed.AddRange(group);
                }

                IEnumerable<Application> minimizedApps = Applications.Where(x => x.Minimized);
                if (minimizedApps.Any())
                {
                    MonitorHeaderRow headerRow = new() { MonitorName = GetString("Minimized_Apps"), SelectString = GetString("SelectAllMinimizedApps") };
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
                return count.ToString(CultureInfo.InvariantCulture) + " " + (count == 1 ? GetString("App") : GetString("Apps"));
            }
        }

        /// <summary>
        /// Call after modifying the Applications list to notify dependent computed properties.
        /// </summary>
        public void NotifyApplicationsChanged()
        {
            OnPropertyChanged(nameof(AppsCountString));
            NotifyMonitorReferencesChanged();
            OnPropertyChanged(nameof(ApplicationsListed));
        }

        internal void NotifyCanBeSavedChanged() => OnPropertyChanged(nameof(CanBeSaved));

        internal void NotifyMonitorReferencesChanged()
        {
            NotifyCanBeSavedChanged();
            OnPropertyChanged(nameof(HasUnresolvedMonitors));
        }

        /// <summary>
        /// Call to refresh the relative time display for LastLaunched.
        /// </summary>
        public void NotifyLastLaunchedChanged()
        {
            OnPropertyChanged(nameof(LastLaunched));
        }

        internal void UpdateLastLaunchedTime(long timestamp)
        {
            if (LastLaunchedTime != timestamp)
            {
                LastLaunchedTime = timestamp;
                OnPropertyChanged(nameof(LastLaunchedTime));
                NotifyLastLaunchedChanged();
            }
        }

        public List<MonitorSetup> Monitors { get; }

        public bool IsPositionChangedManually { get; set; }

        [ObservableProperty]
        [JsonIgnore]
        public partial BitmapImage PreviewIcons { get; set; }

        [ObservableProperty]
        [JsonIgnore]
        public partial BitmapImage PreviewImage { get; set; }

        [ObservableProperty]
        [JsonIgnore]
        public partial double PreviewImageWidth { get; set; }

        public Project()
        {
            Applications = [];
            Monitors = [];
        }

        public Project(Project selectedProject)
            : this(selectedProject, selectedProject)
        {
            IsPositionChangedManually = selectedProject.IsPositionChangedManually;
        }

        internal Project(Project draft, Project previous)
        {
            Id = previous.Id;
            CreationTime = previous.CreationTime;
            LastLaunchedTime = previous.LastLaunchedTime;
            Name = draft.Name;
            EditorWindowTitle = draft.EditorWindowTitle;
            PreviewIcons = draft.PreviewIcons;
            PreviewImage = draft.PreviewImage;
            PreviewImageWidth = draft.PreviewImageWidth;
            IsShortcutNeeded = draft.IsShortcutNeeded;
            MoveExistingWindows = draft.MoveExistingWindows;
            Monitors = draft.Monitors.OrderBy(x => x.MonitorDpiAwareBounds.X).ThenBy(x => x.MonitorDpiAwareBounds.Y).ToList();

            Applications = [];
            foreach (Application item in draft.Applications)
            {
                Application newApp = new(item) { Parent = this };
                newApp.InitializationFinished();
                Applications.Add(newApp);
            }
        }

        public Project(ProjectWrapper project)
        {
            Id = project.Id;
            Name = project.Name;
            CreationTime = project.CreationTime;
            LastLaunchedTime = project.LastLaunchedTime;
            IsShortcutNeeded = project.IsShortcutNeeded;
            MoveExistingWindows = project.MoveExistingWindows;
            Monitors = [];
            Applications = [];

            foreach (ApplicationWrapper app in project.Applications)
            {
                Application newApp = new()
                {
                    Id = string.IsNullOrEmpty(app.Id) ? $"{{{Guid.NewGuid()}}}" : app.Id,
                    AppName = app.Application,
                    AppPath = app.ApplicationPath,
                    AppTitle = app.Title,
                    PwaAppId = string.IsNullOrEmpty(app.PwaAppId) ? string.Empty : app.PwaAppId,
                    Version = string.IsNullOrEmpty(app.Version) ? string.Empty : app.Version,
                    PackageFullName = app.PackageFullName,
                    AppUserModelId = app.AppUserModelId,
                    CommandLineArguments = app.CommandLineArguments,
                    IsElevated = app.IsElevated,
                    CanLaunchElevated = app.CanLaunchElevated,
                    Maximized = app.Maximized,
                    Minimized = app.Minimized,
                    IsNotFound = false,
                    Position = new Application.WindowPosition()
                    {
                        Height = app.Position.Height,
                        Width = app.Position.Width,
                        X = app.Position.X,
                        Y = app.Position.Y,
                    },
                    MonitorNumber = app.Monitor,
                    Parent = this,
                };
                newApp.InitializationFinished();
                Applications.Add(newApp);
            }

            foreach (MonitorConfigurationWrapper monitor in project.MonitorConfiguration)
            {
                Rect dpiAware = new(monitor.MonitorRectDpiAware.Left, monitor.MonitorRectDpiAware.Top, monitor.MonitorRectDpiAware.Width, monitor.MonitorRectDpiAware.Height);
                Rect dpiUnaware = new(monitor.MonitorRectDpiUnaware.Left, monitor.MonitorRectDpiUnaware.Top, monitor.MonitorRectDpiUnaware.Width, monitor.MonitorRectDpiUnaware.Height);
                Monitors.Add(new MonitorSetup(monitor.Id, monitor.InstanceId, monitor.MonitorNumber, monitor.Dpi, dpiAware, dpiUnaware));
            }
        }

        internal static Project FromStorage(ProjectWrapper wrapper)
        {
            var project = new Project(wrapper);
            var usedIds = new HashSet<Guid>();
            for (int i = 0; i < project.Applications.Count; i++)
            {
                var app = project.Applications[i];
                if (!Guid.TryParse(app.Id, out var guid) || !usedIds.Add(guid))
                {
                    do
                    {
                        guid = Guid.NewGuid();
                    }
                    while (!usedIds.Add(guid));
                    app.Id = guid.ToString("B");
                }
                else if (!Guid.TryParseExact(app.Id, "B", out _) && !Guid.TryParseExact(app.Id, "D", out _))
                {
                    app.Id = guid.ToString("B");
                }

                project.NeedsApplicationIdRepair |= app.Id != wrapper.Applications[i].Id;
            }

            return project;
        }

        public void InitializePreview()
        {
            try
            {
                if (Applications == null || Applications.Count == 0 || Monitors == null || Monitors.Count == 0 || HasUnresolvedMonitors)
                {
                    PreviewImage = null;
                    PreviewImageWidth = 0;
                    return;
                }

                // Compute bounding rect across all monitors
                double left = Monitors.Min(m => m.MonitorDpiAwareBounds.X);
                double top = Monitors.Min(m => m.MonitorDpiAwareBounds.Y);
                double right = Monitors.Max(m => m.MonitorDpiAwareBounds.X + m.MonitorDpiAwareBounds.Width);
                double bottom = Monitors.Max(m => m.MonitorDpiAwareBounds.Y + m.MonitorDpiAwareBounds.Height);
                if (right <= left || bottom <= top)
                {
                    return;
                }

                var bounds = new System.Drawing.Rectangle((int)left, (int)top, (int)(right - left), (int)(bottom - top));

                bool isDarkTheme = Helpers.ThemeHelper.IsDarkTheme();

                PreviewImage = Utils.DrawHelper.DrawPreview(this, bounds, isDarkTheme);
                PreviewImageWidth = bounds.Width * 0.1;
                PreviewIcons = Utils.DrawHelper.DrawPreviewIcons(this);
            }
            catch (System.Exception ex)
            {
                ManagedCommon.Logger.LogError("Preview render failed", ex);
            }
        }

        public MonitorSetup GetMonitorForApp(Application app)
        {
            return Monitors.FirstOrDefault(m => m.MonitorNumber == app.MonitorNumber)
                ?? new MonitorSetup(string.Empty, string.Empty, app.MonitorNumber, 96, default, default);
        }

        public void CloseExpanders()
        {
            foreach (Application app in Applications)
            {
                app.IsExpanded = false;
            }
        }

        public void UpdateAfterLaunchAndEdit(Project projectBefore) => UpdateAfterLaunchAndEdit(projectBefore, projectBefore);

        internal void UpdateAfterLaunchAndEdit(Project beforeCapture, Project currentDraft)
        {
            Id = currentDraft.Id;
            Name = currentDraft.Name;
            IsRevertEnabled = true;
            IsShortcutNeeded = currentDraft.IsShortcutNeeded;
            MoveExistingWindows = currentDraft.MoveExistingWindows;
            IsPositionChangedManually = currentDraft.IsPositionChangedManually;

            var liveRows = ReferenceEquals(beforeCapture, currentDraft) ? null :
                ApplicationWindowMatcher.Match(beforeCapture.Applications, currentDraft.Applications)
                    .Where(pair => pair.Previous != null).ToDictionary(pair => pair.Previous, pair => pair.Updated);
            var previousIds = beforeCapture.Applications.Select(app => app.IdWithoutBraces).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var capturedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (app, previous) in ApplicationWindowMatcher.Match(beforeCapture.Applications, Applications))
            {
                // An unmatched window may carry an old stamp shared by multiple windows.
                // Reusing it would also transfer that old row's stored metadata to this window.
                while (string.IsNullOrEmpty(app.Id) || (previous == null && previousIds.Contains(app.IdWithoutBraces)) ||
                    !capturedIds.Add(app.IdWithoutBraces))
                {
                    app.Id = Guid.NewGuid().ToString("B");
                }

                if (previous != null)
                {
                    var current = liveRows?.GetValueOrDefault(previous) ?? previous;
                    app.ApplyDraftSettings(previous, current);
                }
            }
        }

        private static string GetString(string key)
        {
            return ResourceLoaderInstance.ResourceLoader?.GetString(key) ?? key;
        }
    }
}
