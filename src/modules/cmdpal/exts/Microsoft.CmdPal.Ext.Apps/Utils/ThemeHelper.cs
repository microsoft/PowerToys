// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;

using Microsoft.Win32;

namespace Microsoft.CmdPal.Ext.Apps.Utils;

public static class ThemeHelper
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

        // Retrieve the registry value, which is a DWORD (0 or 1)
        var registryValueObj = Registry.GetValue(registryKey, registryValue, null);
        if (registryValueObj != null)
        {
            // 0 = Dark mode, 1 = Light mode
            var isLightMode = Convert.ToBoolean((int)registryValueObj, CultureInfo.InvariantCulture);
            return !isLightMode; // Invert because 0 = Dark
        }
        else
        {
            // Default to Light theme if the registry key is missing
            return false; // Default to dark mode assumption
        }
    }

    public static Theme GetHighContrastBaseType()
    {
        const string registryKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes";
        const string registryValue = "CurrentTheme";

        var themePath = (string?)Registry.GetValue(registryKey, registryValue, string.Empty);
        if (string.IsNullOrEmpty(themePath))
        {
            return Theme.Light; // Default to light theme if missing
        }

        var theme = themePath.Split('\\').Last().Split('.').First().ToLowerInvariant();

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
