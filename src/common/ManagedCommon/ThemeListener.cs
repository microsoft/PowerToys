// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Management;
using System.Security.Principal;

namespace ManagedCommon
{
    /// <summary>
    /// The Delegate for a ThemeChanged Event.
    /// </summary>
    /// <param name="sender">Sender ThemeListener</param>
    public delegate void ThemeChangedEvent(ThemeListener sender);

    public class ThemeListener : IDisposable
    {
        /// <summary>
        /// Gets the Current Theme.
        /// </summary>
        public Theme CurrentTheme { get; private set; }

        /// <summary>
        /// Gets the App Theme.
        /// </summary>
        public AppTheme AppTheme { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the current theme is high contrast.
        /// </summary>
        public bool IsHighContrast
        {
            get { return CurrentTheme != Theme.System; }
        }

        /// <summary>
        /// An event that fires if the Theme changes.
        /// </summary>
        public event ThemeChangedEvent ThemeChanged;

        private readonly ManagementEventWatcher watcher;

        public ThemeListener()
        {
            var currentUser = WindowsIdentity.GetCurrent();
            var query = new WqlEventQuery(
                $"SELECT * FROM RegistryValueChangeEvent WHERE Hive='HKEY_USERS' AND " +
                $"((KeyPath='{currentUser.User.Value}\\\\{ThemeHelpers.HkeyWindowsPersonalizeTheme.Replace("\\", "\\\\")}' AND ValueName='{ThemeHelpers.HValueAppTheme}')" +
                $"OR" +
                $"(KeyPath='{currentUser.User.Value}\\\\{ThemeHelpers.HkeyWindowsTheme.Replace("\\", "\\\\")}' AND ValueName='{ThemeHelpers.HValueCurrentTheme}'))");
            watcher = new ManagementEventWatcher(query);
            watcher.EventArrived += Watcher_EventArrived;
            watcher.Start();

            CurrentTheme = ThemeHelpers.GetCurrentTheme();
            AppTheme = ThemeHelpers.GetAppTheme();
        }

        private void Watcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            var currentTheme = ThemeHelpers.GetCurrentTheme();
            var appTheme = ThemeHelpers.GetAppTheme();

            if (currentTheme != CurrentTheme || appTheme != AppTheme)
            {
                CurrentTheme = currentTheme;
                AppTheme = appTheme;

                ThemeChanged?.Invoke(this);
            }
        }

        public void Dispose()
        {
            watcher.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
