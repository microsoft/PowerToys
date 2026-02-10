// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.OOBE.Views;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;
using WinUIEx;

namespace Microsoft.PowerToys.Settings.UI
{
    public sealed partial class ScoobeWindow : WindowEx
    {
        public static Action<Type> OpenMainWindowCallback { get; set; }

        public static void SetOpenMainWindowCallback(Action<Type> implementation)
        {
            OpenMainWindowCallback = implementation;
        }

        /// <summary>
        /// Gets the list of release groups loaded from GitHub (grouped by major.minor version).
        /// </summary>
        public IList<IList<PowerToysReleaseInfo>> ReleaseGroups { get; private set; }

        private bool _isLoading;

        public ScoobeWindow()
        {
            App.ThemeService.ThemeChanged += OnThemeChanged;
            App.ThemeService.ApplyTheme();

            this.InitializeComponent();

            SetTitleBar();

            SetOpenMainWindowCallback((Type type) =>
            {
                App.OpenSettingsWindow(type);
            });
        }

        private void SetTitleBar()
        {
            WindowHelpers.ForceTopBorder1PixelInsetOnWindows10(WindowNative.GetWindowHandle(this));
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);
            Title = ResourceLoaderInstance.ResourceLoader.GetString("ScoobeWindow_Title");
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            // Set window icon
            this.SetIcon("Assets\\Settings\\icon.ico");
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            if (App.GetSettingsWindow() is MainWindow mainWindow)
            {
                mainWindow.CloseHiddenWindow();
            }

            App.ThemeService.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged(object sender, ElementTheme theme)
        {
            WindowHelper.SetTheme(this, theme);
        }

        private async void NavigationView_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadReleasesAsync();
        }

        private async Task LoadReleasesAsync()
        {
            if (_isLoading)
            {
                return;
            }

            _isLoading = true;
            LoadingProgressRing.Visibility = Visibility.Visible;
            ErrorInfoBar.IsOpen = false;
            navigationView.MenuItems.Clear();

            try
            {
                var releases = await FetchReleasesFromGitHubAsync();
                ReleaseGroups = GroupReleasesByMajorMinor(releases);
                PopulateNavigationItems();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load releases", ex);
                ErrorInfoBar.IsOpen = true;
            }
            finally
            {
                LoadingProgressRing.Visibility = Visibility.Collapsed;
                _isLoading = false;
            }
        }

        private static async Task<IList<PowerToysReleaseInfo>> FetchReleasesFromGitHubAsync()
        {
            using var proxyClientHandler = new HttpClientHandler
            {
                DefaultProxyCredentials = CredentialCache.DefaultCredentials,
                Proxy = WebRequest.GetSystemWebProxy(),
                PreAuthenticate = true,
            };

            using var httpClient = new HttpClient(proxyClientHandler);
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "PowerToys");

            string json = await httpClient.GetStringAsync("https://api.github.com/repos/microsoft/PowerToys/releases?per_page=20");
            var allReleases = JsonSerializer.Deserialize<IList<PowerToysReleaseInfo>>(json, SourceGenerationContextContext.Default.IListPowerToysReleaseInfo);

            if (allReleases is null || allReleases.Count == 0)
            {
                return [];
            }

            return allReleases
                .OrderByDescending(r => r.PublishedDate)
                .ToList();
        }

        private static IList<IList<PowerToysReleaseInfo>> GroupReleasesByMajorMinor(IList<PowerToysReleaseInfo> releases)
        {
            return releases
                .GroupBy(GetMajorMinorVersion)
                .Select(g => g.OrderByDescending(r => r.PublishedDate).ToList() as IList<PowerToysReleaseInfo>)
                .ToList();
        }

        private static string GetMajorMinorVersion(PowerToysReleaseInfo release)
        {
            string version = GetVersionFromRelease(release);
            var parts = version.Split('.');
            if (parts.Length >= 2)
            {
                return $"{parts[0]}.{parts[1]}";
            }

            return version;
        }

        private static string GetVersionFromRelease(PowerToysReleaseInfo release)
        {
            // TagName is typically like "v0.96.0", Name might be "Release v0.96.0"
            string version = release.TagName ?? release.Name ?? "Unknown";
            if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                version = version.Substring(1);
            }

            return version;
        }

        private void PopulateNavigationItems()
        {
            if (ReleaseGroups == null || ReleaseGroups.Count == 0)
            {
                return;
            }

            foreach (var releaseGroup in ReleaseGroups)
            {
                var viewModel = new ScoobeReleaseGroupViewModel(releaseGroup);
                navigationView.MenuItems.Add(viewModel);
            }

            // Select the first item to trigger navigation
            navigationView.SelectedItem = navigationView.MenuItems[0];
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is ScoobeReleaseGroupViewModel viewModel)
            {
                NavigationFrame.Navigate(typeof(ScoobeReleaseNotesPage), viewModel.Releases);
            }
        }

        private async void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadReleasesAsync();
        }

        private void NavigationView_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
        {
            if (args.DisplayMode == NavigationViewDisplayMode.Compact || args.DisplayMode == NavigationViewDisplayMode.Minimal)
            {
                TitleBarIcon.Margin = new Thickness(0, 0, 8, 0); // Workaround, see XAML comment
                AppTitleBar.IsPaneToggleButtonVisible = true;
            }
            else
            {
                TitleBarIcon.Margin = new Thickness(16, 0, 0, 0);  // Workaround, see XAML comment
                AppTitleBar.IsPaneToggleButtonVisible = false;
            }
        }

        private void TitleBar_PaneButtonClick(Microsoft.UI.Xaml.Controls.TitleBar sender, object args)
        {
            navigationView.IsPaneOpen = !navigationView.IsPaneOpen;
        }
    }
}
