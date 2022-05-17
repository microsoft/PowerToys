// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace ManagedCommon
{
    // Based on https://stackoverflow.com/a/62811758/5001796
    public static class ThemeHelpers
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const string HKeyRoot = "HKEY_CURRENT_USER";
        private const string HkeyWindowsTheme = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";
        private const string HValueAppTheme = "AppsUseLightTheme";

        // based on https://stackoverflow.com/questions/51334674/how-to-detect-windows-10-light-dark-mode-in-win32-application
        public static CurrentTheme GetSystemTheme()
        {
            int value = (int)Registry.GetValue($"{HKeyRoot}\\{HkeyWindowsTheme}", HValueAppTheme, 1);
            return (CurrentTheme)value;
        }

        public static bool SupportsImmersiveDarkMode()
        {
            return Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 18985;
        }

        public static void SetImmersiveDarkMode(IntPtr window, bool enabled)
        {
            if (SupportsImmersiveDarkMode())
            {
                int useImmersiveDarkMode = enabled ? 1 : 0;
                _ = DwmSetWindowAttribute(window, 20, ref useImmersiveDarkMode, sizeof(int));
            }
        }

        public static void RegisterForImmersiveDarkMode(IntPtr window)
        {
            SetImmersiveDarkMode(window, GetSystemTheme() == CurrentTheme.Dark);

            var currentUser = WindowsIdentity.GetCurrent();
            var query = new WqlEventQuery($"SELECT * FROM RegistryValueChangeEvent WHERE Hive='HKEY_USERS' AND KeyPath='{currentUser.User.Value}\\\\{HkeyWindowsTheme.Replace("\\", "\\\\")}' AND ValueName='{HValueAppTheme}'");
            var watcher = new ManagementEventWatcher(query);
            watcher.EventArrived += (_, _) => SetImmersiveDarkMode(window, GetSystemTheme() == CurrentTheme.Dark);
            watcher.Start();
        }
    }
}
