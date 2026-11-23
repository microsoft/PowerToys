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
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.OOBE.Views;
using Microsoft.PowerToys.Settings.UI.SerializationContext;
using Microsoft.UI.Windowing;
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

        internal const int MaxReleaseFetchAttempts = 3;

        internal static readonly TimeSpan InitialReleaseFetchRetryDelay = TimeSpan.FromSeconds(1);

        private static readonly TimeSpan ReleaseFetchTimeout = TimeSpan.FromSeconds(30);

        private bool _isLoading;

        public ScoobeWindow()
        {
            App.ThemeService.ThemeChanged += OnThemeChanged;
            App.ThemeService.ApplyTheme();

            this.InitializeComponent();

            WindowHelper.SetTheme(this, App.ThemeService.Theme);
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
            if (Content is FrameworkElement rootElement)
            {
                rootElement.ActualThemeChanged += OnActualThemeChanged;
                OnActualThemeChanged(rootElement, null);
            }

            Title = ResourceLoaderInstance.ResourceLoader.GetString("ScoobeWindow_Title");
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            TitleBarHelper.SetPreferredTheme(
                this,
                sender.ActualTheme == ElementTheme.Dark ? TitleBarTheme.Dark : TitleBarTheme.Light);
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
            if (Content is FrameworkElement rootElement)
            {
                rootElement.ActualThemeChanged -= OnActualThemeChanged;
            }
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
                ReleaseGroups = CreateReleaseGroups(releases, ShouldShowPrereleases());
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

            using var httpClient = new HttpClient(proxyClientHandler) { Timeout = ReleaseFetchTimeout };
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "PowerToys");

            string json = await RetryWithBackoffAsync(
                () => httpClient.GetStringAsync("https://api.github.com/repos/microsoft/PowerToys/releases?per_page=100"),
                Task.Delay);
            var allReleases = JsonSerializer.Deserialize<IList<PowerToysReleaseInfo>>(json, SourceGenerationContextContext.Default.IListPowerToysReleaseInfo);

            if (allReleases is null || allReleases.Count == 0)
            {
                return [];
            }

            return allReleases
                .OrderByDescending(r => r.PublishedDate)
                .ToList();
        }

        internal static async Task<T> RetryWithBackoffAsync<T>(Func<Task<T>> operation, Func<TimeSpan, Task> delay)
        {
            var retryDelay = InitialReleaseFetchRetryDelay;
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex) when (attempt < MaxReleaseFetchAttempts && IsTransientFetchFailure(ex))
                {
                    Logger.LogWarning($"Loading releases failed (attempt {attempt} of {MaxReleaseFetchAttempts}), retrying in {retryDelay.TotalSeconds}s: {ex.Message}");
                    await delay(retryDelay);
                    retryDelay *= 2;
                }
            }
        }

        // Rate limiting (403) and other client errors won't resolve within seconds, so only retry
        // network failures, timeouts, and server-side errors.
        internal static bool IsTransientFetchFailure(Exception ex) => ex switch
        {
            HttpRequestException { StatusCode: null } => true,
            HttpRequestException { StatusCode: HttpStatusCode code } =>
                code is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)code >= 500,
            TaskCanceledException => true,
            _ => false,
        };

        internal static IList<IList<PowerToysReleaseInfo>> CreateReleaseGroups(IList<PowerToysReleaseInfo> releases, bool showPrereleases)
        {
            var groups = new List<IList<PowerToysReleaseInfo>>();
            if (showPrereleases)
            {
                var previewReleases = releases
                    .Where(r => r.IsPrerelease)
                    .OrderByDescending(r => r.PublishedDate)
                    .Take(10)
                    .ToList();
                if (previewReleases.Count > 0)
                {
                    groups.Add(previewReleases);
                }
            }

            var stableGroups = releases
                .Where(r => !r.IsPrerelease)
                .OrderByDescending(r => r.PublishedDate)
                .Take(20)
                .GroupBy(GetMajorMinorVersion)
                .Select(g => g.OrderByDescending(r => r.PublishedDate).ToList() as IList<PowerToysReleaseInfo>)
                .ToList();
            foreach (var stableGroup in stableGroups)
            {
                groups.Add(stableGroup);
            }

            return groups;
        }

        private static bool ShouldShowPrereleases()
        {
            var generalSettings = SettingsRepository<GeneralSettings>.GetInstance(SettingsUtils.Default).SettingsConfig;
            bool isPreviewBuild = string.Equals(
                global::PowerToys.Interop.CommonManaged.GetProductVersionChannel(),
                "preview",
                StringComparison.OrdinalIgnoreCase);
            return generalSettings.IncludePrereleaseUpdates || isPreviewBuild;
        }

        private static string GetMajorMinorVersion(PowerToysReleaseInfo release)
        {
            string version = ScoobeReleaseGroupViewModel.GetVersionFromRelease(release);
            var parts = version.Split('.');
            if (parts.Length >= 2)
            {
                return $"{parts[0]}.{parts[1]}";
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
                AppTitleBar.IsPaneToggleButtonVisible = true;
            }
            else
            {
                AppTitleBar.IsPaneToggleButtonVisible = false;
            }
        }

        private void TitleBar_PaneButtonClick(Microsoft.UI.Xaml.Controls.TitleBar sender, object args)
        {
            navigationView.IsPaneOpen = !navigationView.IsPaneOpen;
        }

        private void OpenSettingsItem_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            App.OpenSettingsWindow(ensurePageIsSelected: true);
        }
    }
}
