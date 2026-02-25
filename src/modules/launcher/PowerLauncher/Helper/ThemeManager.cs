// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ManagedCommon;
using Microsoft.Win32;
using Wox.Infrastructure.Image;
using Wox.Infrastructure.UserSettings;
using Wox.Plugin.Logger;

namespace PowerLauncher.Helper
{
    public class ThemeManager : IDisposable
    {
        private readonly PowerToysRunSettings _settings;
        private readonly MainWindow _mainWindow;
        private readonly ThemeHelper _themeHelper = new();

        private bool _disposed;
        private CancellationTokenSource _themeUpdateTokenSource;
        private const int MaxRetries = 5;
        private const int InitialDelayMs = 2000;

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

            // Cancel any existing theme update operation
            _themeUpdateTokenSource?.Cancel();
            _themeUpdateTokenSource?.Dispose();
            _themeUpdateTokenSource = new CancellationTokenSource();

            // Start theme update with retry logic in the background
            _ = UpdateThemeWithRetryAsync(newTheme, _themeUpdateTokenSource.Token);
        }

        /// <summary>
        /// Applies the theme with retry logic for desktop composition errors.
        /// </summary>
        /// <param name="theme">The theme to apply.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        private async Task UpdateThemeWithRetryAsync(Theme theme, CancellationToken cancellationToken)
        {
            var delayMs = 0;
            const int maxAttempts = MaxRetries + 1;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    if (delayMs > 0)
                    {
                        await Task.Delay(delayMs, cancellationToken);
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        Log.Debug("Theme update operation was cancelled.", typeof(ThemeManager));
                        return;
                    }

                    await _mainWindow.Dispatcher.InvokeAsync(() =>
                    {
                        SetSystemTheme(theme);
                    });

                    if (attempt > 1)
                    {
                        Log.Info($"Successfully applied theme after {attempt - 1} retry attempt(s).", typeof(ThemeManager));
                    }

                    return;
                }
                catch (COMException ex) when (ExceptionHelper.IsRecoverableDwmCompositionException(ex))
                {
                    switch (attempt)
                    {
                        case 1:
                            Log.Warn($"Desktop composition is disabled (HRESULT: 0x{ex.HResult:X}). Scheduling retries for theme update.", typeof(ThemeManager));
                            delayMs = InitialDelayMs;
                            break;
                        case < maxAttempts:
                            Log.Warn($"Retry {attempt - 1}/{MaxRetries} failed: Desktop composition still disabled. Retrying in {delayMs * 2}ms...", typeof(ThemeManager));
                            delayMs *= 2;
                            break;
                        default:
                            Log.Exception($"Failed to set theme after {MaxRetries} retry attempts. Desktop composition remains disabled.", ex, typeof(ThemeManager));
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    Log.Debug("Theme update operation was cancelled.", typeof(ThemeManager));
                    return;
                }
                catch (Exception ex)
                {
                    Log.Exception($"Unexpected error during theme update (attempt {attempt}/{maxAttempts}): {ex.Message}", ex, typeof(ThemeManager));
                    throw;
                }
            }
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
                _themeUpdateTokenSource?.Cancel();
                _themeUpdateTokenSource?.Dispose();
            }

            _disposed = true;
        }
    }
}
