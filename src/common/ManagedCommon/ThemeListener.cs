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

    public partial class ThemeListener : IDisposable
    {
        /// <summary>
        /// Gets the App Theme.
        /// </summary>
        public AppTheme AppTheme { get; private set; }

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
                $"KeyPath='{currentUser.User.Value}\\\\{ThemeHelpers.HkeyWindowsPersonalizeTheme.Replace("\\", "\\\\")}' AND ValueName='{ThemeHelpers.HValueAppTheme}'");
            watcher = new ManagementEventWatcher(query);
            watcher.EventArrived += Watcher_EventArrived;
            watcher.Start();

            AppTheme = ThemeHelpers.GetAppTheme();
        }

        private void Watcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            var appTheme = ThemeHelpers.GetAppTheme();

            if (appTheme != AppTheme)
            {
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
