// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
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
        private ManagedCommon.Theme _currentTheme;
        private bool _disposed;

        public ManagedCommon.Theme CurrentTheme => _currentTheme;

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

        private void SetSystemTheme(ManagedCommon.Theme theme)
        {
            _mainWindow.Background = OSVersionHelper.IsWindows11() is false ? SystemColors.WindowBrush : null;

            // Need to disable WPF0001 since setting Application.Current.ThemeMode is experimental
            // https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net90#set-in-code
#pragma warning disable WPF0001
            Application.Current.ThemeMode = theme is ManagedCommon.Theme.Light ? ThemeMode.Light : ThemeMode.Dark;
            if (theme is ManagedCommon.Theme.Dark or ManagedCommon.Theme.Light)
            {
                if (!OSVersionHelper.IsWindows11())
                {
                    // Apply background only on Windows 10
                    // Windows theme does not work properly for dark and light mode so right now set the background color manual.
                    _mainWindow.Background = new SolidColorBrush
                    {
                        Color = theme is ManagedCommon.Theme.Dark ? (Color)ColorConverter.ConvertFromString("#202020") : (Color)ColorConverter.ConvertFromString("#fafafa"),
                    };
                }
            }
            else
            {
                string styleThemeString = theme switch
                {
                    ManagedCommon.Theme.Light => "Themes/Light.xaml",
                    ManagedCommon.Theme.Dark => "Themes/Dark.xaml",
                    ManagedCommon.Theme.HighContrastOne => "Themes/HighContrast1.xaml",
                    ManagedCommon.Theme.HighContrastTwo => "Themes/HighContrast2.xaml",
                    ManagedCommon.Theme.HighContrastWhite => "Themes/HighContrastWhite.xaml",
                    _ => "Themes/HighContrastBlack.xaml",
                };
                _mainWindow.Resources.MergedDictionaries.Clear();
                _mainWindow.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri(styleThemeString, UriKind.Relative),
                });
                ResourceDictionary test = new ResourceDictionary
                {
                    Source = new Uri(styleThemeString, UriKind.Relative),
                };
                if (OSVersionHelper.IsWindows11())
                {
                    // Apply background only on Windows 11 to keep the same style as WPFUI
                    _mainWindow.Background = new SolidColorBrush
                    {
                        Color = (Color)_mainWindow.FindResource("LauncherBackgroundColor"), // Use your DynamicResource key here
                    };
                }
            }

            ImageLoader.UpdateIconPath(theme);
            ThemeChanged?.Invoke(_currentTheme, theme);
            _currentTheme = theme;
        }

        public void UpdateTheme()
        {
            ManagedCommon.Theme newTheme = _settings.Theme;
            ManagedCommon.Theme theme = ThemeExtensions.GetHighContrastBaseType();
            if (theme != ManagedCommon.Theme.Light)
            {
                newTheme = theme;
            }
            else if (_settings.Theme == ManagedCommon.Theme.System)
            {
                newTheme = ThemeExtensions.GetCurrentTheme();
            }

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
