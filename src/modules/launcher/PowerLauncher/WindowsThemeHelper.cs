namespace Wox.Core.Resource
{
    using System;
    using System.Linq;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Win32;
    using Windows.UI.ViewManagement;

    public static class WindowsThemeHelper
    {
        public static bool IsHighContrastEnabled()
        {
            return SystemParameters.HighContrast;
        }

        public static bool AppsUseLightTheme()
        {
            var uiSettings = new global::Windows.UI.ViewManagement.UISettings();
            var themeColor = ConvertColor(uiSettings.GetColorValue(UIColorType.Background));
            if (themeColor.Equals(Colors.White))
            {
                return true;
            }
            else
            {
                return false;
            }

        }

        public static string getAppTheme()
        {
            try
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
            }catch(Exception e)
            {
                return "Light";
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

        public static Color GetWindowsAccentColor()
        {
            var uiSettings = new global::Windows.UI.ViewManagement.UISettings();
            var accentColor = ConvertColor(uiSettings.GetColorValue(UIColorType.Accent));
            return accentColor;
        }

        public static Color GetWindowsHighLightColor()
        {
            Color accentColor;
            var uiSettings = new global::Windows.UI.ViewManagement.UISettings();
            if (AppsUseLightTheme())
            {
                accentColor = ConvertColor(uiSettings.GetColorValue(UIColorType.AccentLight2));
            }
            else
            {
                accentColor = ConvertColor(uiSettings.GetColorValue(UIColorType.AccentDark2));

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