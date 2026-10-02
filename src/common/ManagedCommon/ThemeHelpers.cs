// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
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

        private const string BaseColorLight = "Light";
        private const string BaseColorDark = "Dark";

        // based on https://stackoverflow.com/questions/51334674/how-to-detect-windows-10-light-dark-mode-in-win32-application
        public static AppTheme GetAppTheme()
        {
            int value = (int)Registry.GetValue($"{HKeyRoot}\\{HkeyWindowsPersonalizeTheme}", HValueAppTheme, 1);
            return (AppTheme)value;
        }

        /// <summary>
        /// Gets the Windows base color, "Light" or "Dark", without depending on WPF.
        /// Same logic as ControlzEx 6.0.0 WindowsThemeHelper.GetWindowsBaseColor(): outside of high contrast it follows
        /// the "AppsUseLightTheme" setting, in high contrast it uses the brightness of the high contrast window color.
        /// </summary>
        public static string GetWindowsBaseColor()
        {
            if (!IsHighContrastEnabled())
            {
                return AppsUseLightTheme() ? BaseColorLight : BaseColorDark;
            }

            // GetSysColor returns a COLORREF: red in the low-order byte, then green, then blue.
            uint windowColor = NativeMethods.GetSysColor(NativeMethods.COLOR_WINDOW);
            return GetBaseColorFromWindowColor((byte)windowColor, (byte)(windowColor >> 8), (byte)(windowColor >> 16));
        }

        /// <summary>
        /// Gets a value indicating whether a Windows high contrast theme is active.
        /// </summary>
        public static bool IsHighContrastEnabled()
        {
            var highContrast = new NativeMethods.HIGHCONTRAST { cbSize = (uint)Marshal.SizeOf<NativeMethods.HIGHCONTRAST>() };
            return NativeMethods.SystemParametersInfo(NativeMethods.SPI_GETHIGHCONTRAST, highContrast.cbSize, ref highContrast, 0)
                && (highContrast.dwFlags & NativeMethods.HCF_HIGHCONTRASTON) != 0;
        }

        /// <summary>
        /// Classifies a window background color as "Light" or "Dark" by its HSL lightness,
        /// the value System.Drawing.Color.GetBrightness() returns.
        /// </summary>
        public static string GetBaseColorFromWindowColor(byte red, byte green, byte blue)
        {
            int max = Math.Max(red, Math.Max(green, blue));
            int min = Math.Min(red, Math.Min(green, blue));
            float brightness = (max + min) / (byte.MaxValue * 2f);
            return brightness < .5 ? BaseColorDark : BaseColorLight;
        }

        public static void SetImmersiveDarkMode(IntPtr window, bool enabled)
        {
            int useImmersiveDarkMode = enabled ? 1 : 0;
            _ = DwmSetWindowAttribute(window, DWMWAImmersiveDarkMode, ref useImmersiveDarkMode, sizeof(int));
        }

        private static bool AppsUseLightTheme()
        {
            // Like ControlzEx, a missing or unreadable value means the light theme.
            try
            {
                object value = Registry.GetValue($"{HKeyRoot}\\{HkeyWindowsPersonalizeTheme}", HValueAppTheme, null);
                return value is null || Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}
