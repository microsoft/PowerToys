// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;

using ManagedCommon;
using Microsoft.Win32;

namespace PowerLauncher.Helper
{
    public static class ThemeExtensions
    {
        public static Theme GetCurrentTheme()
        {
            // Check for high-contrast mode
            Theme highContrastTheme = GetHighContrastBaseType();
            if (highContrastTheme != Theme.Light)
            {
                return highContrastTheme;
            }

            // Check if the system is using dark or light mode
            return IsSystemDarkMode() ? Theme.Dark : Theme.Light;
        }

        private static bool IsSystemDarkMode()
        {
            const string registryKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            const string registryValue = "AppsUseLightTheme";

            object registryValueObj = Registry.GetValue(registryKey, registryValue, null);
            if (registryValueObj != null)
            {
                // The registry value should be a DWORD of 0x0 for Dark mode or 0x1 for Light mode.
                int themeValue = Convert.ToInt32(registryValueObj, CultureInfo.InvariantCulture);
                return themeValue == 0;
            }

            // Default to Light mode if the value could not be retrieved.
            return false;
        }

        public static Theme GetHighContrastBaseType()
        {
            const string registryKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes";
            const string registryValue = "CurrentTheme";

            string themePath = (string)Registry.GetValue(registryKey, registryValue, string.Empty);
            if (string.IsNullOrEmpty(themePath))
            {
                return Theme.Light; // Default to light theme if missing
            }

            string theme = themePath.Split('\\').Last().Split('.').First().ToLowerInvariant();

            return theme switch
            {
                "hc1" => Theme.HighContrastOne,
                "hc2" => Theme.HighContrastTwo,
                "hcwhite" => Theme.HighContrastWhite,
                "hcblack" => Theme.HighContrastBlack,
                _ => Theme.Light,
            };
        }
    }
}
