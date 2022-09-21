// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ManagedCommon
{
    // Based on https://stackoverflow.com/a/62811758/5001796
    public static class ThemeHelpers
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        internal const string HKeyRoot = "HKEY_CURRENT_USER";
        internal const string HkeyWindowsTheme = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes";
        internal const string HkeyWindowsPersonalizeTheme = $@"{HkeyWindowsTheme}\Personalize";
        internal const string HValueAppTheme = "AppsUseLightTheme";
        internal const int DWMWAImmersiveDarkMode = 20;

        // based on https://stackoverflow.com/questions/51334674/how-to-detect-windows-10-light-dark-mode-in-win32-application
        public static AppTheme GetAppTheme()
        {
            int value = (int)Registry.GetValue($"{HKeyRoot}\\{HkeyWindowsPersonalizeTheme}", HValueAppTheme, 1);
            return (AppTheme)value;
        }

        public static void SetImmersiveDarkMode(IntPtr window, bool enabled)
        {
            int useImmersiveDarkMode = enabled ? 1 : 0;
            _ = DwmSetWindowAttribute(window, DWMWAImmersiveDarkMode, ref useImmersiveDarkMode, sizeof(int));
        }
    }
}
