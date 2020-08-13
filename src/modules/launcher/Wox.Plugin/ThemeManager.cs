// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Windows;
using ControlzEx.Theming;
using MahApps.Metro.Theming;
using Microsoft.Win32;

namespace Wox.Plugin
{
    public class ThemeManager : IDisposable
    {
        private readonly Application _app;
        private const string LightTheme = "Light.Accent1";
        private const string DarkTheme = "Dark.Accent1";
        private const string HighContrastOneTheme = "HighContrast.Accent2";
        private const string HighContrastTwoTheme = "HighContrast.Accent3";
        private const string HighContrastBlackTheme = "HighContrast.Accent4";
        private const string HighContrastWhiteTheme = "HighContrast.Accent5";

        private Theme currentTheme;
        private bool _disposed = false;

        public event ThemeChangedHandler ThemeChanged;

        public ThemeManager(Application app)
        {
            _app = app;

            Uri highContrastOneThemeUri = new Uri("pack://application:,,,/Themes/HighContrast1.xaml");
            Uri highContrastTwoThemeUri = new Uri("pack://application:,,,/Themes/HighContrast2.xaml");
            Uri highContrastBlackThemeUri = new Uri("pack://application:,,,/Themes/HighContrastWhite.xaml");
            Uri highContrastWhiteThemeUri = new Uri("pack://application:,,,/Themes/HighContrastBlack.xaml");
            Uri lightThemeUri = new Uri("pack://application:,,,/Themes/Light.xaml");
            Uri darkThemeUri = new Uri("pack://application:,,,/Themes/Dark.xaml");

            ControlzEx.Theming.ThemeManager.Current.AddLibraryTheme(
                new LibraryTheme(
                    highContrastOneThemeUri,
                    MahAppsLibraryThemeProvider.DefaultInstance));
            ControlzEx.Theming.ThemeManager.Current.AddLibraryTheme(
                new LibraryTheme(
                    highContrastTwoThemeUri,
                    MahAppsLibraryThemeProvider.DefaultInstance));
            ControlzEx.Theming.ThemeManager.Current.AddLibraryTheme(
                new LibraryTheme(
                    highContrastBlackThemeUri,
                    MahAppsLibraryThemeProvider.DefaultInstance));
            ControlzEx.Theming.ThemeManager.Current.AddLibraryTheme(
                new LibraryTheme(
                    highContrastWhiteThemeUri,
                    MahAppsLibraryThemeProvider.DefaultInstance));
            ControlzEx.Theming.ThemeManager.Current.AddLibraryTheme(
                new LibraryTheme(
                    lightThemeUri,
                    MahAppsLibraryThemeProvider.DefaultInstance));
            ControlzEx.Theming.ThemeManager.Current.AddLibraryTheme(
                new LibraryTheme(
                    darkThemeUri,
                    MahAppsLibraryThemeProvider.DefaultInstance));

            ResetTheme();
            ControlzEx.Theming.ThemeManager.Current.ThemeSyncMode = ThemeSyncMode.SyncWithAppMode;
            ControlzEx.Theming.ThemeManager.Current.ThemeChanged += Current_ThemeChanged;
            SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
        }

        private void SystemParameters_StaticPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SystemParameters.HighContrast))
            {
                ResetTheme();
            }
        }

        public Theme GetCurrentTheme()
        {
            return currentTheme;
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
                    return Theme.None;
            }
        }

        private void ResetTheme()
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
            Theme oldTheme = currentTheme;
            if (theme == currentTheme)
            {
                return;
            }

            if (theme == Theme.HighContrastOne)
            {
                ControlzEx.Theming.ThemeManager.Current.ChangeTheme(_app, HighContrastOneTheme);
                currentTheme = Theme.HighContrastOne;
            }
            else if (theme == Theme.HighContrastTwo)
            {
                ControlzEx.Theming.ThemeManager.Current.ChangeTheme(_app, HighContrastTwoTheme);
                currentTheme = Theme.HighContrastTwo;
            }
            else if (theme == Theme.HighContrastWhite)
            {
                ControlzEx.Theming.ThemeManager.Current.ChangeTheme(_app, HighContrastWhiteTheme);
                currentTheme = Theme.HighContrastWhite;
            }
            else if (theme == Theme.HighContrastBlack)
            {
                ControlzEx.Theming.ThemeManager.Current.ChangeTheme(_app, HighContrastBlackTheme);
                currentTheme = Theme.HighContrastBlack;
            }
            else if (theme == Theme.Light)
            {
                ControlzEx.Theming.ThemeManager.Current.ChangeTheme(_app, LightTheme);
                currentTheme = Theme.Light;
            }
            else if (theme == Theme.Dark)
            {
                ControlzEx.Theming.ThemeManager.Current.ChangeTheme(_app, DarkTheme);
                currentTheme = Theme.Dark;
            }
            else
            {
                currentTheme = Theme.None;
            }

            ThemeChanged?.Invoke(oldTheme, currentTheme);
        }

        private void Current_ThemeChanged(object sender, ThemeChangedEventArgs e)
        {
            ResetTheme();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    ControlzEx.Theming.ThemeManager.Current.ThemeChanged -= Current_ThemeChanged;
                    SystemParameters.StaticPropertyChanged -= SystemParameters_StaticPropertyChanged;
                    _disposed = true;
                }
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public delegate void ThemeChangedHandler(Theme oldTheme, Theme newTheme);

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
