namespace Wox.Core.Resource
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Media;
    using JetBrains.Annotations;
    using Microsoft.Win32;
    using Windows.UI.ViewManagement;

    public static class WindowsThemeHelper
    {
        public static bool IsHighContrastEnabled()
        {
            return SystemParameters.HighContrast;
        }

        [MustUseReturnValue]
        public static bool AppsUseLightTheme()
        {
            Color? themeColor = null;
            try
            {
                var uiSettings = new global::Windows.UI.ViewManagement.UISettings();
                themeColor = ConvertColor(uiSettings.GetColorValue(UIColorType.Background));
                if (themeColor.Equals(Colors.White))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
            }

            return true;
        }

        [MustUseReturnValue]
        public static string getAppTheme()
        {
            if (IsHighContrastEnabled())
            {
                return GetHighContrastBaseType();
            }
            else
            {
                if (AppsUseLightTheme())
                {
                    return "Light";
                }
                else
                {
                    return "Dark";
                }
            }
        }

        public static string GetHighContrastBaseType()
        {
            string RegistryKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes";
            string theme = (string)Registry.GetValue(RegistryKey, "CurrentTheme", string.Empty);
            theme = theme.Split('\\').Last().Split('.').First().ToString();

            if (theme == "hc1")
                return "HighContrast1";
            else if (theme == "hc2")
                return "HighContrast2";
            else if (theme == "hcwhite")
                return "HighContrastWhite";
            else if (theme == "hcblack")
                return "HighContrastBlack";
            else
                return "";
        }

        [CanBeNull]
        [MustUseReturnValue]
        public static Color? GetWindowsAccentColor()
        {
            Color? accentColor = null;

            try
            {
                var uiSettings = new global::Windows.UI.ViewManagement.UISettings();
                accentColor = ConvertColor(uiSettings.GetColorValue(UIColorType.Accent));
                return accentColor;
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
            }

            return accentColor;
        }


        private static Color ConvertColor(global::Windows.UI.Color color)
        {
            //Convert the specified UWP color to a WPF color
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }
    }
}