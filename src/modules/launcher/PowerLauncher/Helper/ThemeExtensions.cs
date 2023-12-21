// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using ManagedCommon;
using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace PowerLauncher.Helper
{
    public static class ThemeExtensions
    {
        public static Theme ToTheme(this ApplicationTheme applicationTheme)
        {
            return applicationTheme switch
            {
                ApplicationTheme.Dark => Theme.Dark,
                ApplicationTheme.Light => Theme.Light,
                ApplicationTheme.HighContrast => GetHighContrastBaseType(),
                _ => Theme.Light,
            };
        }

        private static Theme GetHighContrastBaseType()
        {
            string registryKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes";
            string theme = (string)Registry.GetValue(registryKey, "CurrentTheme", string.Empty);
            theme = theme.Split('\\').Last().Split('.').First().ToString();

            switch (theme)
            {
                case "hc1":
                    return Theme.HighContrastOne;
                case "hc2":
                    return Theme.HighContrastTwo;
                case "hcwhite":
                    return Theme.HighContrastWhite;
                case "hcblack":
                    return Theme.HighContrastBlack;
                default:
                    return Theme.HighContrastOne;
            }
        }
    }
}
