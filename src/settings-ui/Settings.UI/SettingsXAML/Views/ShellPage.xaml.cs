// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common.Search;
using Common.Search.FuzzSearch;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Controls;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Settings.UI.Library;
using Windows.Data.Json;
using Windows.System;
using WinRT.Interop;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// Root page.
    /// </summary>
    public sealed partial class ShellPage : UserControl, IDisposable
    {
        /// <summary>
        /// Declaration for the ipc callback function.
        /// </summary>
        /// <param name="msg">message.</param>
        public delegate void IPCMessageCallback(string msg);

        /// <summary>
        /// Declaration for opening main window callback function.
        /// </summary>
        public delegate void MainOpeningCallback(Type type);

        /// <summary>
        /// Declaration for updating the general settings callback function.
        /// </summary>
        public delegate bool UpdatingGeneralSettingsCallback(ModuleType moduleType, bool isEnabled);

        /// <summary>
        /// Declaration for opening oobe window callback function.
        /// </summary>
        public delegate void OobeOpeningCallback();

        /// <summary>
        /// Declaration for opening whats new window callback function.
        /// </summary>
        public delegate void WhatIsNewOpeningCallback();

        /// <summary>
        /// Declaration for opening flyout window callback function.
        /// </summary>
        public delegate void FlyoutOpeningCallback(POINT? point);

        /// <summary>
        /// Declaration for disabling hide of flyout window callback function.
        /// </summary>
        public delegate void DisablingFlyoutHidingCallback();

        /// <summary>
        /// Gets or sets a shell handler to be used to update contents of the shell dynamically from page within the frame.
        /// </summary>
        public static ShellPage ShellHandler { get; set; }

        /// <summary>
        /// Gets or sets iPC default callback function.
        /// </summary>
        public static IPCMessageCallback DefaultSndMSGCallback { get; set; }

        /// <summary>
        /// Gets or sets iPC callback function for restart as admin.
        /// </summary>
        public static IPCMessageCallback SndRestartAsAdminMsgCallback { get; set; }

        /// <summary>
        /// Gets or sets iPC callback function for checking updates.
        /// </summary>
        public static IPCMessageCallback CheckForUpdatesMsgCallback { get; set; }

        /// <summary>
        /// Gets or sets callback function for opening main window
        /// </summary>
        public static MainOpeningCallback OpenMainWindowCallback { get; set; }

        /// <summary>
        /// Gets or sets callback function for updating the general settings
        /// </summary>
        public static UpdatingGeneralSettingsCallback UpdateGeneralSettingsCallback { get; set; }

        /// <summary>
        /// Gets or sets callback function for opening oobe window
        /// </summary>
        public static OobeOpeningCallback OpenOobeWindowCallback { get; set; }

        /// <summary>
        /// Gets or sets callback function for opening oobe window
        /// </summary>
        public static WhatIsNewOpeningCallback OpenWhatIsNewWindowCallback { get; set; }

        /// <summary>
        /// Gets or sets callback function for opening flyout window
        /// </summary>
        public static FlyoutOpeningCallback OpenFlyoutCallback { get; set; }

        /// <summary>
        /// Gets or sets callback function for disabling hide of flyout window
        /// </summary>
        public static DisablingFlyoutHidingCallback DisableFlyoutHidingCallback { get; set; }

        /// <summary>
        /// Gets view model.
        /// </summary>
        public ShellViewModel ViewModel { get; }

        /// <summary>
        /// Gets a collection of functions that handle IPC responses.
        /// </summary>
        public List<System.Action<JsonObject>> IPCResponseHandleList { get; } = new List<System.Action<JsonObject>>();

        public static bool IsElevated { get; set; }

        public static bool IsUserAnAdmin { get; set; }

        public Controls.TitleBar TitleBar => AppTitleBar;

        private Dictionary<Type, NavigationViewItem> _navViewParentLookup = new Dictionary<Type, NavigationViewItem>();
        private List<string> _searchSuggestions = [];

        private CancellationTokenSource _searchDebounceCts;
        private const int SearchDebounceMs = 500;
        private bool _disposed;

        // Removed trace id counter per cleanup

        /// <summary>
        /// Initializes a new instance of the <see cref="ShellPage"/> class.
        /// Shell page constructor.
        /// </summary>
        public ShellPage()
        {
            InitializeComponent();
            SetWindowTitle();
            var settingsUtils = SettingsUtils.Default;
            ViewModel = new ShellViewModel(SettingsRepository<GeneralSettings>.GetInstance(settingsUtils));
            DataContext = ViewModel;
            ShellHandler = this;
            ViewModel.Initialize(shellFrame, navigationView, KeyboardAccelerators);

            // NL moved navigation to general page to the moment when the window is first activated (to not make flyout window disappear)
            // shellFrame.Navigate(typeof(GeneralPage));
            IPCResponseHandleList.Add(ReceiveMessage);
            IPCResponseService.Instance.RegisterForIPC();

            if (_navViewParentLookup.Count > 0)
            {
                _navViewParentLookup.Clear();
            }

            var topLevelItems = navigationView.MenuItems.OfType<NavigationViewItem>().ToArray();

            foreach (var parent in topLevelItems)
            {
                foreach (var child in parent.MenuItems.OfType<NavigationViewItem>())
                {
                    _navViewParentLookup.TryAdd(child.GetValue(NavHelper.NavigateToProperty) as Type, parent);
                    _searchSuggestions.Add(child.Content?.ToString());
                }
            }
        }

        public static int SendDefaultIPCMessage(string msg)
        {
            DefaultSndMSGCallback?.Invoke(msg);
            return 0;
        }

        public static int SendCheckForUpdatesIPCMessage(string msg)
        {
            CheckForUpdatesMsgCallback?.Invoke(msg);

            return 0;
        }

        public static int SendRestartAdminIPCMessage(string msg)
        {
            SndRestartAsAdminMsgCallback?.Invoke(msg);
            return 0;
        }

        /// <summary>
        /// Set Default IPC Message callback function.
        /// </summary>
        /// <param name="implementation">delegate function implementation.</param>
        public static void SetDefaultSndMessageCallback(IPCMessageCallback implementation)
        {
            DefaultSndMSGCallback = implementation;
        }

        /// <summary>
        /// Set restart as admin IPC callback function.
        /// </summary>
        /// <param name="implementation">delegate function implementation.</param>
        public static void SetRestartAdminSndMessageCallback(IPCMessageCallback implementation)
        {
            SndRestartAsAdminMsgCallback = implementation;
        }

        /// <summary>
        /// Set check for updates IPC callback function.
        /// </summary>
        /// <param name="implementation">delegate function implementation.</param>
        public static void SetCheckForUpdatesMessageCallback(IPCMessageCallback implementation)
        {
            CheckForUpdatesMsgCallback = implementation;
        }

        /// <summary>
        /// Set main window opening callback function
        /// </summary>
        /// <param name="implementation">delegate function implementation.</param>
        public static void SetOpenMainWindowCallback(MainOpeningCallback implementation)
        {
            OpenMainWindowCallback = implementation;
        }

        /// <summary>
        /// Set updating the general settings callback function
        /// </summary>
        /// <param name="implementation">delegate function implementation.</param>
        public static void SetUpdatingGeneralSettingsCallback(UpdatingGeneralSettingsCallback implementation)
        {
            UpdateGeneralSettingsCallback = implementation;
        }

        /// <summary>
        /// Set oobe opening callback function
        /// </summary>
        /// <param name="implementation">delegate function implementation.</param>
        public static void SetOpenOobeCallback(OobeOpeningCallback implementation)
        {
            OpenOobeWindowCallback = implementation;
        }

        /// <summary>
        /// Set whats new opening callback function
        /// </summary>
        /// <param name="implementation">delegate function implementation.</param>
        public static void SetOpenWhatIsNewCallback(WhatIsNewOpeningCallback implementation)
        {
            OpenWhatIsNewWindowCallback = implementation;
        }

        /// <summary>
        /// Set flyout opening callback function
        /// </summary>
        /// <param name="implementation">delegate function implementation.</param>
        public static void SetOpenFlyoutCallback(FlyoutOpeningCallback implementation)
        {
            OpenFlyoutCallback = implementation;
        }

        /// <summary>
        /// Set disable flyout hiding callback function
        /// </summary>
        /// <param name="implementation">delegate function implementation.</param>
        public static void SetDisableFlyoutHidingCallback(DisablingFlyoutHidingCallback implementation)
        {
            DisableFlyoutHidingCallback = implementation;
        }

        public static void SetElevationStatus(bool isElevated)
        {
            IsElevated = isElevated;
        }

        public static void SetIsUserAnAdmin(bool isAdmin)
        {
            IsUserAnAdmin = isAdmin;
        }

        public static void Navigate(Type type)
        {
            NavigationService.Navigate(type);
        }

        public void Refresh()
        {
            shellFrame.Navigate(typeof(DashboardPage));
        }

        // Tell the current page view model to update
        public void SignalGeneralDataUpdate()
        {
            IRefreshablePage currentPage = shellFrame?.Content as IRefreshablePage;
            if (currentPage != null)
            {
                currentPage.RefreshEnabledState();
            }
        }

        private bool navigationViewInitialStateProcessed; // avoid announcing initial state of the navigation pane.

        private void NavigationView_PaneOpened(NavigationView sender, object args)
        {
            if (!navigationViewInitialStateProcessed)
            {
                navigationViewInitialStateProcessed = true;
                return;
            }

            var peer = FrameworkElementAutomationPeer.FromElement(sender);
            if (peer == null)
            {
                peer = FrameworkElementAutomationPeer.CreatePeerForElement(sender);
            }

            if (AutomationPeer.ListenerExists(AutomationEvents.MenuOpened))
            {
                var loader = ResourceLoaderInstance.ResourceLoader;
                peer.RaiseNotificationEvent(
                    AutomationNotificationKind.ActionCompleted,
                    AutomationNotificationProcessing.ImportantMostRecent,
                    loader.GetString("Shell_NavigationMenu_Announce_Open"),
                    "navigationMenuPaneOpened");
            }
        }

        private void NavigationView_PaneClosed(NavigationView sender, object args)
        {
            if (!navigationViewInitialStateProcessed)
            {
                navigationViewInitialStateProcessed = true;
                return;
            }

            var peer = FrameworkElementAutomationPeer.FromElement(sender);
            if (peer == null)
            {
                peer = FrameworkElementAutomationPeer.CreatePeerForElement(sender);
            }

            if (AutomationPeer.ListenerExists(AutomationEvents.MenuClosed))
            {
                var loader = ResourceLoaderInstance.ResourceLoader;
                peer.RaiseNotificationEvent(
                    AutomationNotificationKind.ActionCompleted,
                    AutomationNotificationProcessing.ImportantMostRecent,
                    loader.GetString("Shell_NavigationMenu_Announce_Collapse"),
                    "navigationMenuPaneClosed");
            }
        }

        private void OOBEItem_Tapped(object sender, TappedRoutedEventArgs e)
        {
            OpenOobeWindowCallback();
        }

        private async void FeedbackItem_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://aka.ms/powerToysGiveFeedback"));
        }

        private void WhatIsNewItem_Tapped(object sender, TappedRoutedEventArgs e)
        {
            OpenWhatIsNewWindowCallback();
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            NavigationViewItem selectedItem = args.SelectedItem as NavigationViewItem;
            if (selectedItem != null)
            {
                Type pageType = selectedItem.GetValue(NavHelper.NavigateToProperty) as Type;

                if (pageType != null && _navViewParentLookup.TryGetValue(pageType, out var parentItem) && !parentItem.IsExpanded)
                {
                    parentItem.IsExpanded = true;
                    ViewModel.Expanding = parentItem;
                    NavigationService.Navigate(pageType);
                }
            }
        }

        private void ReceiveMessage(JsonObject json)
        {
            if (json != null)
            {
                IJsonValue whatToShowJson;
                if (json.TryGetValue("ShowYourself", out whatToShowJson))
                {
                    if (whatToShowJson.ValueType == JsonValueType.String && whatToShowJson.GetString().Equals("flyout", StringComparison.Ordinal))
                    {
                        POINT? p = null;

                        IJsonValue flyoutPointX;
                        IJsonValue flyoutPointY;
                        if (json.TryGetValue("x_position", out flyoutPointX) && json.TryGetValue("y_position", out flyoutPointY))
                        {
                            if (flyoutPointX.ValueType == JsonValueType.Number && flyoutPointY.ValueType == JsonValueType.Number)
                            {
                                int flyout_x = (int)flyoutPointX.GetNumber();
                                int flyout_y = (int)flyoutPointY.GetNumber();
                                p = new POINT(flyout_x, flyout_y);
                            }
                        }

                        OpenFlyoutCallback(p);
                    }
                    else if (whatToShowJson.ValueType == JsonValueType.String)
                    {
                        OpenMainWindowCallback(App.GetPage(whatToShowJson.GetString()));
                    }
                }
            }
        }

        internal static void EnsurePageIsSelected()
        {
            NavigationService.EnsurePageIsSelected(typeof(DashboardPage));
        }

        private void SetWindowTitle()
        {
            var loader = ResourceLoaderInstance.ResourceLoader;
            AppTitleBar.Title = App.IsElevated ? loader.GetString("SettingsWindow_AdminTitle") : loader.GetString("SettingsWindow_Title");
#if DEBUG
            AppTitleBar.Subtitle = "Debug";
#endif
        }

        private void ShellPage_Loaded(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                SearchIndexService.BuildIndex();
            })
            .ContinueWith(_ => { });
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
            navigationView.IsPaneOpen = !navigationView.IsPaneOpen;
        }

        private async void Close_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            await CloseDialog.ShowAsync();
        }

        private void CloseDialog_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            const string ptTrayIconWindowClass = "PToyTrayIconWindow"; // Defined in runner/tray_icon.h
            const nuint ID_CLOSE_MENU_COMMAND = 40001;                  // Generated resource from runner/runner.base.rc

            // Exit the XAML application
            Application.Current.Exit();

            // Invoke the exit command from the tray icon
            IntPtr hWnd = NativeMethods.FindWindow(ptTrayIconWindowClass, ptTrayIconWindowClass);
            NativeMethods.SendMessage(hWnd, NativeMethods.WM_COMMAND, ID_CLOSE_MENU_COMMAND, 0);
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
                return;
            }

            try
            {
                await Task.Delay(SearchDebounceMs, token);
            }
            catch (TaskCanceledException)
            {
                return; // debounce canceled
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            // Query the index on a background thread to avoid blocking UI
            List<SettingEntry> results = null;
            try
            {
                // If the token is already canceled before scheduling, the task won't start.
                results = await Task.Run(() => SearchIndexService.Search(query, token), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            _lastSearchResults = results;
            _lastQueryText = query;

            var top = BuildSuggestionItems(query, results);

            sender.ItemsSource = top;
            sender.IsSuggestionListOpen = top.Count > 0;
        }

        private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            // Do not navigate on arrow navigation. Let QuerySubmitted handle commits (Enter/click).
            // AutoSuggestBox will pass the chosen item via args.ChosenSuggestion to QuerySubmitted.
            // No action required here.
        }

        private void NavigateFromSuggestion(SuggestionItem item)
        {
            var queryText = _lastQueryText;

            if (item.IsShowAll)
            {
                // Navigate to full results page
                var searchParams = new SearchResultsNavigationParams(queryText, _lastSearchResults);
                NavigationService.Navigate<SearchResultsPage>(searchParams);

                SearchBox.Text = string.Empty;
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

                // Clear the search box after navigation
                SearchBox.Text = string.Empty;
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
            args.Handled = true; // prevent further processing (e.g., unintended navigation)
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var box = sender as AutoSuggestBox;
            var current = box?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(current))
            {
                return; // nothing to restore
            }

            // If current text matches last query and we have results, reconstruct the suggestion list.
            if (string.Equals(current, _lastQueryText, StringComparison.Ordinal) && _lastSearchResults?.Count > 0)
            {
                try
                {
                    var top = BuildSuggestionItems(current, _lastSearchResults);
                    box.ItemsSource = top;
                    box.IsSuggestionListOpen = top.Count > 0;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error restoring suggestion list {ex.Message}");
                }
            }
        }

        // Centralized suggestion projection logic used by TextChanged & GotFocus restore.
        private List<SuggestionItem> BuildSuggestionItems(string query, List<SettingEntry> results)
        {
            results ??= new();
            if (results.Count == 0)
            {
                var rl = ResourceLoaderInstance.ResourceLoader;
                var noResultsPrefix = rl.GetString("Shell_Search_NoResults");
                if (string.IsNullOrEmpty(noResultsPrefix))
                {
                    noResultsPrefix = "No results for";
                }

                var headerText = $"{noResultsPrefix} '{query}'";
                return new List<SuggestionItem>
                {
                    new()
                    {
                        Header = headerText,
                        IsNoResults = true,
                    },
                };
            }

            var list = results.Take(5).Select(e =>
            {
                string subtitle = string.Empty;
                if (e.Type != EntryType.SettingsPage)
                {
                    subtitle = SearchIndexService.GetLocalizedPageName(e.PageTypeName);
                    if (string.IsNullOrEmpty(subtitle))
                    {
                        subtitle = SearchIndexService.Index
                            .Where(x => x.Type == EntryType.SettingsPage && x.PageTypeName == e.PageTypeName)
                            .Select(x => x.Header)
                            .FirstOrDefault() ?? string.Empty;
                    }
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
            }).ToList();

            if (results.Count > 5)
            {
                list.Add(new SuggestionItem { IsShowAll = true });
            }

            return list;
        }

        private async void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            // If a suggestion is selected, navigate directly
            if (args.ChosenSuggestion is SuggestionItem chosen)
            {
                NavigateFromSuggestion(chosen);
                return;
            }

            var queryText = (args.QueryText ?? _lastQueryText)?.Trim();
            if (string.IsNullOrWhiteSpace(queryText))
            {
                NavigationService.Navigate<DashboardPage>();
                return;
            }

            // Prefer cached results (from live search); if empty, perform a fresh search
            var matched = _lastSearchResults?.Count > 0 && string.Equals(_lastQueryText, queryText, StringComparison.Ordinal)
                ? _lastSearchResults
                : await Task.Run(() => SearchIndexService.Search(queryText));

            var searchParams = new SearchResultsNavigationParams(queryText, matched);
            NavigationService.Navigate<SearchResultsPage>(searchParams);
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
