// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Windows;
using ManagedCommon;
using Microsoft.Win32;

namespace Microsoft.PowerToys.Common.UI
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
        private bool _disposed;

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
                new ControlzEx.Theming.LibraryTheme(
                    highContrastOneThemeUri,
                    CustomLibraryThemeProvider.DefaultInstance));
            ControlzEx.Theming.ThemeManager.Current.AddLibraryTheme(
                new ControlzEx.Theming.LibraryTheme(
                    highContrastTwoThemeUri,
                    CustomLibraryThemeProvider.DefaultInstance));
            ControlzEx.Theming.ThemeManager.Current.AddLibraryTheme(
                new ControlzEx.Theming.LibraryTheme(
                    highContrastBlackThemeUri,
                    CustomLibraryThemeProvider.DefaultInstance));
            ControlzEx.Theming.ThemeManager.Current.AddLibraryTheme(
                new ControlzEx.Theming.LibraryTheme(
                    highContrastWhiteThemeUri,
                    CustomLibraryThemeProvider.DefaultInstance));
            ControlzEx.Theming.ThemeManager.Current.AddLibraryTheme(
                new ControlzEx.Theming.LibraryTheme(
                    lightThemeUri,
                    CustomLibraryThemeProvider.DefaultInstance));
            ControlzEx.Theming.ThemeManager.Current.AddLibraryTheme(
                new ControlzEx.Theming.LibraryTheme(
                    darkThemeUri,
                    CustomLibraryThemeProvider.DefaultInstance));

            ResetTheme();
            ControlzEx.Theming.ThemeManager.Current.ThemeSyncMode = ControlzEx.Theming.ThemeSyncMode.SyncWithAppMode;
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
                    return Theme.HighContrastOne;
            }
        }

        private void ResetTheme()
        {
            ChangeTheme(currentTheme, false);
        }

        public void ChangeTheme(Theme theme, bool forceSystem)
        {
            Theme oldTheme = currentTheme;

            if (theme == Theme.System)
            {
                currentTheme = Theme.System;
                if (ControlzEx.Theming.WindowsThemeHelper.IsHighContrastEnabled())
                {
                    Theme highContrastBaseType = GetHighContrastBaseType();
                    ChangeTheme(highContrastBaseType, true);
                }
                else
                {
                    string baseColor = ControlzEx.Theming.WindowsThemeHelper.GetWindowsBaseColor();
                    ChangeTheme((Theme)Enum.Parse(typeof(Theme), baseColor), true);
                }
            }
            else if (theme == Theme.HighContrastOne)
            {
                currentTheme = Theme.HighContrastOne;
                ControlzEx.Theming.ThemeManager.Current.ChangeTheme(_app, HighContrastOneTheme, true);
            }
            else if (theme == Theme.HighContrastTwo)
            {
                currentTheme = Theme.HighContrastTwo;
                ControlzEx.Theming.ThemeManager.Current.ChangeTheme(_app, HighContrastTwoTheme, true);
            }
            else if (theme == Theme.HighContrastWhite)
            {
                currentTheme = Theme.HighContrastWhite;
                ControlzEx.Theming.ThemeManager.Current.ChangeTheme(_app, HighContrastWhiteTheme, true);
            }
            else if (theme == Theme.HighContrastBlack)
            {
                currentTheme = Theme.HighContrastBlack;
                ControlzEx.Theming.ThemeManager.Current.ChangeTheme(_app, HighContrastBlackTheme, true);
            }
            else if (theme == Theme.Light)
            {
                currentTheme = Theme.Light;
                ControlzEx.Theming.ThemeManager.Current.ChangeTheme(_app, LightTheme);
            }
            else if (theme == Theme.Dark)
            {
                currentTheme = Theme.Dark;
                ControlzEx.Theming.ThemeManager.Current.ChangeTheme(_app, DarkTheme);
            }

            ThemeChanged?.Invoke(oldTheme, currentTheme);

            if (forceSystem)
            {
                currentTheme = Theme.System;
            }
        }

        private void Current_ThemeChanged(object sender, ControlzEx.Theming.ThemeChangedEventArgs e)
        {
            ControlzEx.Theming.ThemeManager.Current.ThemeChanged -= Current_ThemeChanged;
            try
            {
                ResetTheme();
            }
            finally
            {
                ControlzEx.Theming.ThemeManager.Current.ThemeChanged += Current_ThemeChanged;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    ControlzEx.Theming.ThemeManager.Current.ThemeChanged -= Current_ThemeChanged;
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
}
