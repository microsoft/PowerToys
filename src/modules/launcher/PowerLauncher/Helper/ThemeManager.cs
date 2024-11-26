// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Windows;
using ControlzEx.Theming;
using ManagedCommon;
using Microsoft.Office.Interop.OneNote;
using Microsoft.Win32;
using UnitsNet;
using Wox.Infrastructure.Image;
using Wox.Infrastructure.UserSettings;
using static PowerLauncher.Helper.WindowsInteropHelper;

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
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General)
            {
                // Theme has changed, update resources
                UpdateTheme();
            }
        }

        private void SetSystemTheme(ManagedCommon.Theme theme)
        {
#pragma warning disable WPF0001
            if (theme == ManagedCommon.Theme.Dark)
            {
                _mainWindow.ThemeMode = ThemeMode.Dark;
                ImageLoader.UpdateIconPath(ManagedCommon.Theme.Dark);
                ThemeChanged?.Invoke(ManagedCommon.Theme.Dark, ManagedCommon.Theme.Light);
            }
            else if (theme == ManagedCommon.Theme.Light)
            {
                _mainWindow.ThemeMode = ThemeMode.Light;
                ImageLoader.UpdateIconPath(ManagedCommon.Theme.Light);
                ThemeChanged?.Invoke(ManagedCommon.Theme.Light, ManagedCommon.Theme.Dark);
            }
            else
            {
                ImageLoader.UpdateIconPath(theme);
                _mainWindow.ThemeMode = ThemeMode.None;
                string themeString = theme switch
                {
                    ManagedCommon.Theme.HighContrastOne => "Themes/HighContrast1.xaml",
                    ManagedCommon.Theme.HighContrastTwo => "Themes/HighContrast2.xaml",
                    ManagedCommon.Theme.HighContrastWhite => "Themes/HighContrastWhite.xaml",
                    _ => "Themes/HighContrastBlack.xaml",
                };
                if (_mainWindow.Resources.Contains("SystemColorWindowColorBrush"))
                {
                    _mainWindow.Resources.Remove("SystemColorWindowColorBrush");
                }

                _mainWindow.Resources.MergedDictionaries.Clear();
                if (_mainWindow.Resources.Contains("SystemColorWindowColorBrush"))
                {
                    _mainWindow.Resources.Remove("SystemColorWindowColorBrush");
                }

                /* _mainWindow.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.xaml", UriKind.Absolute),
                });*/
                _mainWindow.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("Styles/Styles.xaml", UriKind.Relative),
                });
                if (_mainWindow.Resources.Contains("SystemColorWindowColorBrush"))
                {
                    _mainWindow.Resources.Remove("SystemColorWindowColorBrush");
                }

                _mainWindow.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri(themeString, UriKind.Relative),
                });
            }
        }

        public void UpdateTheme()
        {
            _currentTheme = _settings.Theme;
            ManagedCommon.Theme theme = ThemeExtensions.GetHighContrastBaseType();
            if (theme != ManagedCommon.Theme.Light)
            {
                _currentTheme = theme;
            }
            else if (_settings.Theme == ManagedCommon.Theme.System)
            {
                _currentTheme = ThemeExtensions.GetCurrentTheme();
            }

            _mainWindow.Dispatcher.Invoke(() =>
            {
                SetSystemTheme(_currentTheme);
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
