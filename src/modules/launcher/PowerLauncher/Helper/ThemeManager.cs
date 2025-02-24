// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ManagedCommon;
using Microsoft.Win32;
using Wox.Infrastructure.Image;
using Wox.Infrastructure.UserSettings;

namespace PowerLauncher.Helper
{
    public class ThemeManager : IDisposable
    {
        private readonly PowerToysRunSettings _settings;
        private readonly MainWindow _mainWindow;
        private readonly ThemeHelper _themeHelper = new();

        private bool _disposed;

        public Theme CurrentTheme { get; private set; }

        public event Common.UI.ThemeChangedHandler ThemeChanged;

        public ThemeManager(PowerToysRunSettings settings, MainWindow mainWindow)
        {
            _settings = settings;
            _mainWindow = mainWindow;
            UpdateTheme();
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General)
            {
                UpdateTheme();
            }
        }

        private void SetSystemTheme(Theme theme)
        {
            _mainWindow.Background = !OSVersionHelper.IsWindows11() ? SystemColors.WindowBrush : null;

            // Need to disable WPF0001 since setting Application.Current.ThemeMode is experimental
            // https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net90#set-in-code
#pragma warning disable WPF0001
            Application.Current.ThemeMode = theme == Theme.Light ? ThemeMode.Light : ThemeMode.Dark;
#pragma warning restore WPF0001

            if (theme is Theme.Dark or Theme.Light)
            {
                if (!OSVersionHelper.IsWindows11())
                {
                    // Apply background only on Windows 10
                    // Windows theme does not work properly for dark and light mode so right now set the background color manually.
                    _mainWindow.Background = new SolidColorBrush
                    {
                        Color = (Color)ColorConverter.ConvertFromString(theme == Theme.Dark ? "#202020" : "#fafafa"),
                    };
                }
            }
            else
            {
                string styleThemeString = theme switch
                {
                    Theme.HighContrastOne => "Themes/HighContrast1.xaml",
                    Theme.HighContrastTwo => "Themes/HighContrast2.xaml",
                    Theme.HighContrastWhite => "Themes/HighContrastWhite.xaml",
                    Theme.HighContrastBlack => "Themes/HighContrastBlack.xaml",
                    _ => "Themes/Light.xaml",
                };

                _mainWindow.Resources.MergedDictionaries.Clear();
                _mainWindow.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri(styleThemeString, UriKind.Relative),
                });

                if (OSVersionHelper.IsWindows11())
                {
                    // Apply background only on Windows 11 to keep the same style as WPFUI
                    _mainWindow.Background = new SolidColorBrush
                    {
                        Color = (Color)_mainWindow.FindResource("LauncherBackgroundColor"),
                    };
                }
            }

            ImageLoader.UpdateIconPath(theme);
            ThemeChanged?.Invoke(CurrentTheme, theme);
            CurrentTheme = theme;
        }

        /// <summary>
        /// Updates the application's theme based on system settings and user preferences.
        /// </summary>
        /// <remarks>
        /// This considers:
        /// - Whether a High Contrast theme is active in Windows.
        /// - The system-wide app mode preference (Light or Dark).
        /// - The user's preference override for Light or Dark mode in the application settings.
        /// </remarks>
        public void UpdateTheme()
        {
            Theme newTheme = _themeHelper.DetermineTheme(_settings.Theme);

            _mainWindow.Dispatcher.Invoke(() =>
            {
                SetSystemTheme(newTheme);
            });
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            }

            _disposed = true;
        }
    }
}
