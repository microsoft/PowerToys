// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common.Search;
using Common.Search.FuzzSearch;
using ManagedCommon;
using Microsoft.PowerLauncher.Telemetry;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Data.Json;
using WinRT.Interop;
using WinUIEx;

namespace Microsoft.PowerToys.Settings.UI
{
    public sealed partial class MainWindow : WindowEx, IDisposable
    {
        private ISearchService<NavigationViewItem> _searchService;
        private CancellationTokenSource _searchDebounceCts;
        private const int SearchDebounceMs = 500;
        private bool _disposed;

        // Tracing id for correlating logs of a single search interaction
        private static long _searchTraceIdCounter;

        public MainWindow(bool createHidden = false)
        {
            var bootTime = new Stopwatch();
            bootTime.Start();
            this.Activated += Window_Activated_SetIcon;

            App.ThemeService.ThemeChanged += OnThemeChanged;
            App.ThemeService.ApplyTheme();

            ShellPage.SetElevationStatus(App.IsElevated);
            ShellPage.SetIsUserAnAdmin(App.IsUserAnAdmin);

            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var placement = WindowHelper.DeserializePlacementOrDefault(hWnd);
            if (createHidden)
            {
                placement.ShowCmd = NativeMethods.SW_HIDE;

                // Restore the last known placement on the first activation
                this.Activated += Window_Activated;
            }

            NativeMethods.SetWindowPlacement(hWnd, ref placement);

            var loader = ResourceLoaderInstance.ResourceLoader;
            Title = App.IsElevated ? loader.GetString("SettingsWindow_AdminTitle") : loader.GetString("SettingsWindow_Title");

            // send IPC Message
            ShellPage.SetDefaultSndMessageCallback(msg =>
            {
                // IPC Manager is null when launching runner directly
                App.GetTwoWayIPCManager()?.Send(msg);
            });

            // send IPC Message
            ShellPage.SetRestartAdminSndMessageCallback(msg =>
            {
                App.GetTwoWayIPCManager()?.Send(msg);
                Environment.Exit(0); // close application
            });

            // send IPC Message
            ShellPage.SetCheckForUpdatesMessageCallback(msg =>
            {
                App.GetTwoWayIPCManager()?.Send(msg);
            });

            // open main window
            ShellPage.SetOpenMainWindowCallback(type =>
            {
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                     App.OpenSettingsWindow(type));
            });

            // open main window
            ShellPage.SetUpdatingGeneralSettingsCallback((ModuleType moduleType, bool isEnabled) =>
            {
                SettingsRepository<GeneralSettings> repository = SettingsRepository<GeneralSettings>.GetInstance(new SettingsUtils());
                GeneralSettings generalSettingsConfig = repository.SettingsConfig;
                bool needToUpdate = ModuleHelper.GetIsModuleEnabled(generalSettingsConfig, moduleType) != isEnabled;

                if (needToUpdate)
                {
                    ModuleHelper.SetIsModuleEnabled(generalSettingsConfig, moduleType, isEnabled);
                    var outgoing = new OutGoingGeneralSettings(generalSettingsConfig);
                    this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                    {
                        ShellPage.SendDefaultIPCMessage(outgoing.ToString());
                        ShellPage.ShellHandler?.SignalGeneralDataUpdate();
                    });
                }

                return needToUpdate;
            });

            // open oobe
            ShellPage.SetOpenOobeCallback(() =>
            {
                if (App.GetOobeWindow() == null)
                {
                    App.SetOobeWindow(new OobeWindow(Microsoft.PowerToys.Settings.UI.OOBE.Enums.PowerToysModules.Overview));
                }

                App.GetOobeWindow().Activate();
            });

            // open whats new window
            ShellPage.SetOpenWhatIsNewCallback(() =>
            {
                if (App.GetOobeWindow() == null)
                {
                    App.SetOobeWindow(new OobeWindow(Microsoft.PowerToys.Settings.UI.OOBE.Enums.PowerToysModules.WhatsNew));
                }
                else
                {
                    App.GetOobeWindow().SetAppWindow(OOBE.Enums.PowerToysModules.WhatsNew);
                }

                App.GetOobeWindow().Activate();
            });

            // open flyout
            ShellPage.SetOpenFlyoutCallback((POINT? p) =>
            {
                this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    if (App.GetFlyoutWindow() == null)
                    {
                        App.SetFlyoutWindow(new FlyoutWindow(p));
                    }

                    FlyoutWindow flyout = App.GetFlyoutWindow();
                    flyout.FlyoutAppearPosition = p;
                    flyout.Activate();

                    // https://github.com/microsoft/microsoft-ui-xaml/issues/7595 - Activate doesn't bring window to the foreground
                    // Need to call SetForegroundWindow to actually gain focus.
                    WindowHelpers.BringToForeground(flyout.GetWindowHandle());
                });
            });

            // disable flyout hiding
            ShellPage.SetDisableFlyoutHidingCallback(() =>
            {
                this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    if (App.GetFlyoutWindow() == null)
                    {
                        App.SetFlyoutWindow(new FlyoutWindow(null));
                    }

                    App.GetFlyoutWindow().ViewModel.DisableHiding();
                });
            });

            this.InitializeComponent();
            SetTitleBar();

            // receive IPC Message
            App.IPCMessageReceivedCallback = (string msg) =>
            {
                if (ShellPage.ShellHandler.IPCResponseHandleList != null)
                {
                    var success = JsonObject.TryParse(msg, out JsonObject json);
                    if (success)
                    {
                        foreach (Action<JsonObject> handle in ShellPage.ShellHandler.IPCResponseHandleList)
                        {
                            handle(json);
                        }
                    }
                    else
                    {
                        Logger.LogError("Failed to parse JSON from IPC message.");
                    }
                }
            };

            bootTime.Stop();

            PowerToysTelemetry.Log.WriteEvent(new SettingsBootEvent() { BootTimeMs = bootTime.ElapsedMilliseconds });
        }

        public void NavigateToSection(System.Type type)
        {
            ShellPage.Navigate(type);
        }

        private void SetTitleBar()
        {
            AppTitleBar.Window = this;
            WindowHelpers.ForceTopBorder1PixelInsetOnWindows10(WindowNative.GetWindowHandle(this));
            var loader = ResourceLoaderInstance.ResourceLoader;
            AppTitleBar.Title = App.IsElevated ? loader.GetString("SettingsWindow_AdminTitle") : loader.GetString("SettingsWindow_Title");

#if DEBUG
            AppTitleBar.Subtitle = "Debug";
#endif
        }

        private void NavigationView_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
        {
            if (args.DisplayMode == NavigationViewDisplayMode.Compact || args.DisplayMode == NavigationViewDisplayMode.Minimal)
            {
                AppTitleBar.IsPaneButtonVisible = true;
            }
            else
            {
                AppTitleBar.IsPaneButtonVisible = false;
            }
        }

        private void PaneToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            shellPage.NavView.IsPaneOpen = !shellPage.NavView.IsPaneOpen;
        }

        public void CloseHiddenWindow()
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            if (!NativeMethods.IsWindowVisible(hWnd))
            {
                Close();
            }
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowHelper.SerializePlacement(hWnd);

            if (App.GetOobeWindow() == null)
            {
                App.ClearSettingsWindow();
            }
            else
            {
                args.Handled = true;
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_HIDE);
            }

            App.ThemeService.ThemeChanged -= OnThemeChanged;
        }

        private void Window_Activated_SetIcon(object sender, WindowActivatedEventArgs args)
        {
            // Set window icon
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.SetIcon("Assets\\Settings\\icon.ico");
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState != WindowActivationState.Deactivated)
            {
                this.Activated -= Window_Activated;
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var placement = WindowHelper.DeserializePlacementOrDefault(hWnd);
                NativeMethods.SetWindowPlacement(hWnd, ref placement);
            }
        }

        private void OnThemeChanged(object sender, ElementTheme theme)
        {
            WindowHelper.SetTheme(this, theme);
        }

        internal void EnsurePageIsSelected()
        {
            ShellPage.EnsurePageIsSelected();
        }

        private void ShellPage_Loaded(object sender, RoutedEventArgs e)
        {
            shellPage.NavView.DisplayModeChanged += NavigationView_DisplayModeChanged;
            _searchService = new FuzzSearchService<NavigationViewItem>(shellPage.ViewModel.NavItems, (NavigationViewItem item) => item.Content.ToString());
        }

        private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            // Do not navigate on arrow navigation. Let QuerySubmitted handle commits (Enter/click).
            // AutoSuggestBox will pass the chosen item via args.ChosenSuggestion to QuerySubmitted.
            // No action required here.
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // do not prompt unless search for text.
            return;
        }

        private async void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            var swSubmit = Stopwatch.StartNew();
            Logger.LogDebug("[Search][Submit] start");

            // If a suggestion is selected, navigate directly
            if (args.ChosenSuggestion is SuggestionItem chosen)
            {
                Logger.LogDebug($"[Search][Submit] chosen suggestion -> navigate to {chosen.PageTypeName} element={chosen.ElementName ?? "<page>"}");
                NavigateFromSuggestion(chosen);
                return;
            }

            var queryText = (args.QueryText ?? _lastQueryText)?.Trim();
            if (string.IsNullOrWhiteSpace(queryText))
            {
                Logger.LogDebug("[Search][Submit] empty query -> navigate Dashboard");
                NavigationService.Navigate<DashboardPage>();
                return;
            }

            // Prefer cached results (from live search); if empty, perform a fresh search
            var matched = _lastSearchResults?.Count > 0 && string.Equals(_lastQueryText, queryText, StringComparison.Ordinal)
                ? _lastSearchResults
                : await Task.Run(() =>
                {
                    var sw = Stopwatch.StartNew();
                    Logger.LogDebug($"[Search][Submit] background search for '{queryText}'...");
                    var r = SearchIndexService.Search(queryText);
                    sw.Stop();
                    Logger.LogDebug($"[Search][Submit] background search done in {sw.ElapsedMilliseconds} ms. results={r?.Count ?? 0}");
                    return r;
                });

            var searchParams = new SearchResultsNavigationParams(queryText, matched);
            Logger.LogDebug($"[Search][Submit] navigate to SearchResultsPage (results={matched?.Count ?? 0})");
            NavigationService.Navigate<SearchResultsPage>(searchParams);
            swSubmit.Stop();
            Logger.LogDebug($"[Search][Submit] total {swSubmit.ElapsedMilliseconds} ms");
        }

        private List<SettingEntry> _lastSearchResults = new();
        private string _lastQueryText = string.Empty;

        private async void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            // Only respond to user input, not programmatic text changes
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            {
                return;
            }

            var query = sender.Text?.Trim() ?? string.Empty;

            var traceId = Interlocked.Increment(ref _searchTraceIdCounter);
            var swOverall = Stopwatch.StartNew();
            Logger.LogDebug($"[Search][TextChanged][{traceId}] start. query='{query}'");

            // Debounce: cancel previous pending search
            _searchDebounceCts?.Cancel();
            _searchDebounceCts?.Dispose();
            _searchDebounceCts = new CancellationTokenSource();
            var token = _searchDebounceCts.Token;

            if (string.IsNullOrWhiteSpace(query))
            {
                sender.ItemsSource = null;
                sender.IsSuggestionListOpen = false;
                _lastSearchResults.Clear();
                _lastQueryText = string.Empty;
                Logger.LogDebug($"[Search][TextChanged][{traceId}] empty query. end");
                return;
            }

            try
            {
                await Task.Delay(SearchDebounceMs, token);
            }
            catch (TaskCanceledException)
            {
                // A newer keystroke arrived; abandon this run
                Logger.LogDebug($"[Search][TextChanged][{traceId}] debounce canceled at +{swOverall.ElapsedMilliseconds} ms");
                return;
            }

            if (token.IsCancellationRequested)
            {
                Logger.LogDebug($"[Search][TextChanged][{traceId}] token canceled post-debounce at +{swOverall.ElapsedMilliseconds} ms");
                return;
            }

            // Query the index on a background thread to avoid blocking UI
            List<SettingEntry> results = null;
            try
            {
                // If the token is already canceled before scheduling, the task won't start.
                var swSearch = Stopwatch.StartNew();
                Logger.LogDebug($"[Search][TextChanged][{traceId}] dispatch search...");
                results = await Task.Run(() => SearchIndexService.Search(query, token), token);
                swSearch.Stop();
                Logger.LogDebug($"[Search][TextChanged][{traceId}] search done in {swSearch.ElapsedMilliseconds} ms. results={results?.Count ?? 0}");
            }
            catch (OperationCanceledException)
            {
                Logger.LogDebug($"[Search][TextChanged][{traceId}] search canceled at +{swOverall.ElapsedMilliseconds} ms");
                return;
            }

            if (token.IsCancellationRequested)
            {
                Logger.LogDebug($"[Search][TextChanged][{traceId}] token canceled after search at +{swOverall.ElapsedMilliseconds} ms");
                return;
            }

            _lastSearchResults = results;
            _lastQueryText = query;

            List<SuggestionItem> top;
            if (results.Count == 0)
            {
                // Explicit no-results row
                var rl = ResourceLoaderInstance.ResourceLoader;
                var noResultsPrefix = rl.GetString("Shell_Search_NoResults");
                if (string.IsNullOrEmpty(noResultsPrefix))
                {
                    noResultsPrefix = "No results for";
                }

                var headerText = $"{noResultsPrefix} '{query}'";
                top = new List<SuggestionItem>
                {
                    new SuggestionItem
                    {
                        Header = headerText,
                        IsNoResults = true,
                    },
                };

                Logger.LogDebug($"[Search][TextChanged][{traceId}] no results -> added placeholder item (count={top.Count})");
            }
            else
            {
                // Project top 5 suggestions
                var swProject = Stopwatch.StartNew();
                top = results.Take(5)
                    .Select(e =>
                    {
                        string subtitle = string.Empty;
                        if (e.Type != EntryType.SettingsPage)
                        {
                            var swSubtitle = Stopwatch.StartNew();
                            subtitle = SearchIndexService.GetLocalizedPageName(e.PageTypeName);
                            if (string.IsNullOrEmpty(subtitle))
                            {
                                // Fallback: look up the module title from the in-memory index
                                var swFallback = Stopwatch.StartNew();
                                subtitle = SearchIndexService.Index
                                    .Where(x => x.Type == EntryType.SettingsPage && x.PageTypeName == e.PageTypeName)
                                    .Select(x => x.Header)
                                    .FirstOrDefault() ?? string.Empty;
                                swFallback.Stop();
                                Logger.LogDebug($"[Search][TextChanged][{traceId}] fallback subtitle for '{e.PageTypeName}' took {swFallback.ElapsedMilliseconds} ms");
                            }

                            swSubtitle.Stop();
                            Logger.LogDebug($"[Search][TextChanged][{traceId}] subtitle for '{e.PageTypeName}' took {swSubtitle.ElapsedMilliseconds} ms");
                        }

                        return new SuggestionItem
                        {
                            Header = e.Header,
                            Icon = e.Icon,
                            PageTypeName = e.PageTypeName,
                            ElementName = e.ElementName,
                            ParentElementName = e.ParentElementName,
                            Subtitle = subtitle,
                            IsShowAll = false,
                        };
                    })
                    .ToList();
                swProject.Stop();
                Logger.LogDebug($"[Search][TextChanged][{traceId}] project suggestions took {swProject.ElapsedMilliseconds} ms. topCount={top.Count}");

                if (results.Count > 5)
                {
                    // Add a tail item to show all results if there are more than 5
                    var rl = ResourceLoaderInstance.ResourceLoader;
                    var showAllText = rl.GetString("Shell_Search_ShowAll");
                    if (string.IsNullOrEmpty(showAllText))
                    {
                        showAllText = "Show all results";
                    }

                    top.Add(new SuggestionItem
                    {
                        Header = showAllText,
                        Icon = "\uE721", // Find
                        Subtitle = string.Empty,
                        IsShowAll = true,
                    });
                    Logger.LogDebug($"[Search][TextChanged][{traceId}] added 'Show all results' item");
                }
            }

            var swUi = Stopwatch.StartNew();
            sender.ItemsSource = top;
            sender.IsSuggestionListOpen = top.Count > 0;
            swUi.Stop();
            swOverall.Stop();
            Logger.LogDebug($"[Search][TextChanged][{traceId}] UI update took {swUi.ElapsedMilliseconds} ms. total={swOverall.ElapsedMilliseconds} ms");
        }

        private void NavigateFromSuggestion(SuggestionItem item)
        {
            var queryText = _lastQueryText;

            if (item.IsShowAll)
            {
                // Navigate to full results page
                var searchParams = new SearchResultsNavigationParams(queryText, _lastSearchResults);
                NavigationService.Navigate<SearchResultsPage>(searchParams);
                return;
            }

            // Navigate to the selected item
            var pageType = GetPageTypeFromName(item.PageTypeName);
            if (pageType != null)
            {
                if (string.IsNullOrEmpty(item.ElementName))
                {
                    NavigationService.Navigate(pageType);
                }
                else
                {
                    var navigationParams = new NavigationParams(item.ElementName, item.ParentElementName);
                    NavigationService.Navigate(pageType, navigationParams);
                }
            }
        }

        private static Type GetPageTypeFromName(string pageTypeName)
        {
            if (string.IsNullOrEmpty(pageTypeName))
            {
                return null;
            }

            var assembly = typeof(GeneralPage).Assembly;
            return assembly.GetType($"Microsoft.PowerToys.Settings.UI.Views.{pageTypeName}");
        }

        private void CtrlF_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            SearchBox.Focus(FocusState.Programmatic);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _searchDebounceCts?.Cancel();
            _searchDebounceCts?.Dispose();
            _searchDebounceCts = null;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
