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
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace Microsoft.PowerToys.Settings.UI.OOBE.Views
{
    public sealed partial class ScoobeShellPage : UserControl
    {
        public static Action<Type> OpenMainWindowCallback { get; set; }

        public static void SetOpenMainWindowCallback(Action<Type> implementation)
        {
            OpenMainWindowCallback = implementation;
        }

        /// <summary>
        /// Gets or sets a shell handler to be used to update contents of the shell dynamically from page within the frame.
        /// </summary>
        public static ScoobeShellPage ScoobeShellHandler { get; set; }

        /// <summary>
        /// Gets the list of release groups loaded from GitHub (grouped by major.minor version).
        /// </summary>
        public IList<IList<PowerToysReleaseInfo>> ReleaseGroups { get; private set; }

        private bool _isLoading;

        public ScoobeShellPage()
        {
            InitializeComponent();
            SetTitleBar();
            ScoobeShellHandler = this;
        }

        private async void ShellPage_Loaded(object sender, RoutedEventArgs e)
        {
            SetTitleBar();
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

            string json = await httpClient.GetStringAsync("https://api.github.com/repos/microsoft/PowerToys/releases");
            var allReleases = JsonSerializer.Deserialize<IList<PowerToysReleaseInfo>>(json, SourceGenerationContextContext.Default.IListPowerToysReleaseInfo);

            return allReleases
                .OrderByDescending(r => r.PublishedDate)
                .ToList();
        }

        private static IList<IList<PowerToysReleaseInfo>> GroupReleasesByMajorMinor(IList<PowerToysReleaseInfo> releases)
        {
            return releases
                .GroupBy(r => GetMajorMinorVersion(r))
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

        private void SetTitleBar()
        {
            var window = App.GetScoobeWindow();
            if (window != null)
            {
                window.ExtendsContentIntoTitleBar = true;
                WindowHelpers.ForceTopBorder1PixelInsetOnWindows10(WindowNative.GetWindowHandle(window));
                window.SetTitleBar(AppTitleBar);
            }
        }

        private void NavigationView_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
        {
            if (args.DisplayMode == NavigationViewDisplayMode.Compact || args.DisplayMode == NavigationViewDisplayMode.Minimal)
            {
                AppTitleBar.Margin = new Thickness(48, 0, 0, 0);
                AppTitleBarText.Margin = new Thickness(12, 0, 0, 0);
            }
            else
            {
                AppTitleBar.Margin = new Thickness(16, 0, 0, 0);
                AppTitleBarText.Margin = new Thickness(16, 0, 0, 0);
            }
        }
    }
}
