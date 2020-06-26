using ControlzEx.Theming;
using MahApps.Metro.Theming;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace Wox.Core.Resource
{
    public class ThemeManager
    {
        private Theme currentTheme;
        private readonly Application App;
        private readonly string LightTheme = "Light.Accent1";
        private readonly string DarkTheme = "Dark.Accent1";
        private readonly string HighContrastOneTheme = "HighContrast.Accent2";
        private readonly string HighContrastTwoTheme = "HighContrast.Accent3";
        private readonly string HighContrastBlackTheme = "HighContrast.Accent4";
        private readonly string HighContrastWhiteTheme = "HighContrast.Accent5";

        public ThemeManager(Application app)
        {
            this.App = app;

            Uri HighContrastOneThemeUri = new Uri("pack://application:,,,/Themes/HighContrast1.xaml");
            Uri HighContrastTwoThemeUri = new Uri("pack://application:,,,/Themes/HighContrast2.xaml");
            Uri HighContrastBlackThemeUri = new Uri("pack://application:,,,/Themes/HighContrastWhite.xaml");
            Uri HighContrastWhiteThemeUri = new Uri("pack://application:,,,/Themes/HighContrastBlack.xaml");
            Uri LightThemeUri = new Uri("pack://application:,,,/Themes/Light.xaml");
            Uri DarkThemeUri = new Uri("pack://application:,,,/Themes/Dark.xaml");

            ControlzEx.Theming.ThemeManager.Current.AddLibraryTheme(
                new LibraryTheme(HighContrastOneThemeUri,
                MahAppsLibraryThemeProvider.DefaultInstance));
            ControlzEx.Theming.ThemeManager.Current.AddLibraryTheme(
                new LibraryTheme(HighContrastTwoThemeUri,
                MahAppsLibraryThemeProvider.DefaultInstance));
            ControlzEx.Theming.ThemeManager.Current.AddLibraryTheme(
                new LibraryTheme(HighContrastBlackThemeUri,
                MahAppsLibraryThemeProvider.DefaultInstance));
            ControlzEx.Theming.ThemeManager.Current.AddLibraryTheme(
                new LibraryTheme(HighContrastWhiteThemeUri,
                MahAppsLibraryThemeProvider.DefaultInstance));
            ControlzEx.Theming.ThemeManager.Current.AddLibraryTheme(
                new LibraryTheme(LightThemeUri,
                MahAppsLibraryThemeProvider.DefaultInstance));
            ControlzEx.Theming.ThemeManager.Current.AddLibraryTheme(
                new LibraryTheme(DarkThemeUri,
                MahAppsLibraryThemeProvider.DefaultInstance));

            ResetTheme();
            ControlzEx.Theming.ThemeManager.Current.ThemeSyncMode = ThemeSyncMode.SyncWithAppMode;
            ControlzEx.Theming.ThemeManager.Current.ThemeChanged += Current_ThemeChanged;
            SystemParameters.StaticPropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(SystemParameters.HighContrast))
                {
                    ResetTheme();
                }
            };
        }
        

        public Theme GetHighContrastBaseType()
        {
            string RegistryKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes";
            string theme = (string) Registry.GetValue(RegistryKey, "CurrentTheme", string.Empty);
            theme = theme.Split('\\').Last().Split('.').First().ToString();

            if (theme == "hc1")
                return Theme.HighContrastOne;
            else if (theme == "hc2")
                return Theme.HighContrastTwo;
            else if (theme == "hcwhite")
                return Theme.HighContrastWhite;
            else if (theme == "hcblack")
                return Theme.HighContrastBlack;
            else
                return Theme.None;
        }

        public void ResetTheme()
        {
            if (SystemParameters.HighContrast)
            {
                Theme highContrastBaseType = GetHighContrastBaseType();
                ChangeTheme(highContrastBaseType);
            }
            else
            {
                string baseColor = WindowsThemeHelper.GetWindowsBaseColor();
                ChangeTheme((Theme)Enum.Parse(typeof(Theme), baseColor));
            }
        }

        private void ChangeTheme(Theme theme)
        {
            if (theme == currentTheme)
                return;
            if (theme == Theme.HighContrastOne)
            {
                ControlzEx.Theming.ThemeManager.Current.ChangeTheme(this.App, this.HighContrastOneTheme);
                currentTheme = Theme.HighContrastOne;
            }
            else if (theme == Theme.HighContrastTwo)
            {
                ControlzEx.Theming.ThemeManager.Current.ChangeTheme(this.App, this.HighContrastTwoTheme);
                currentTheme = Theme.HighContrastTwo;
            }
            else if (theme == Theme.HighContrastWhite)
            {
                ControlzEx.Theming.ThemeManager.Current.ChangeTheme(this.App, this.HighContrastWhiteTheme);
                currentTheme = Theme.HighContrastWhite;
            }
            else if (theme == Theme.HighContrastBlack)
            {
                ControlzEx.Theming.ThemeManager.Current.ChangeTheme(this.App, this.HighContrastBlackTheme);
                currentTheme = Theme.HighContrastBlack;
            }
            else if (theme == Theme.Light)
            {
                ControlzEx.Theming.ThemeManager.Current.ChangeTheme(this.App, this.LightTheme);
                currentTheme = Theme.Light;
            }
            else if (theme == Theme.Dark)
            {
                ControlzEx.Theming.ThemeManager.Current.ChangeTheme(this.App, this.DarkTheme);
                currentTheme = Theme.Dark;
            }
            else
            {
                currentTheme = Theme.None;
            }
            Debug.WriteLine("Theme Changed to :" + currentTheme);
        }

        private void Current_ThemeChanged(object sender, ThemeChangedEventArgs e)
        {
            ResetTheme();
        }
    }

    public enum Theme
    {
        None,
        Light, 
        Dark, 
        HighContrastOne,
        HighContrastTwo,
        HighContrastBlack,
        HighContrastWhite,
    }
}
