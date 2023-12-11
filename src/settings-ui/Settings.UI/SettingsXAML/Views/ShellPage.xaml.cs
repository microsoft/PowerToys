// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Windows.Data.Json;
using Windows.System;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// Root page.
    /// </summary>
    public sealed partial class ShellPage : UserControl
    {
        /// <summary>
        /// Declaration for the ipc callback function.
        /// </summary>
        /// <param name="msg">message.</param>
        public delegate void IPCMessageCallback(string msg);

        /// <summary>
        /// Declaration for the opening main window callback function.
        /// </summary>
        public delegate void MainOpeningCallback(Type type);

        /// <summary>
        /// Declaration for the updating the general settings callback function.
        /// </summary>
        public delegate bool UpdatingGeneralSettingsCallback(ModuleType moduleType, bool isEnabled);

        /// <summary>
        /// Declaration for the opening oobe window callback function.
        /// </summary>
        public delegate void OobeOpeningCallback();

        /// <summary>
        /// Declaration for the opening whats new window callback function.
        /// </summary>
        public delegate void WhatIsNewOpeningCallback();

        /// <summary>
        /// Declaration for the opening flyout window callback function.
        /// </summary>
        public delegate void FlyoutOpeningCallback(POINT? point);

        /// <summary>
        /// Declaration for the disabling hide of flyout window callback function.
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
        public ShellViewModel ViewModel { get; } = new ShellViewModel();

        /// <summary>
        /// Gets a collection of functions that handle IPC responses.
        /// </summary>
        public List<System.Action<JsonObject>> IPCResponseHandleList { get; } = new List<System.Action<JsonObject>>();

        public static bool IsElevated { get; set; }

        public static bool IsUserAnAdmin { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ShellPage"/> class.
        /// Shell page constructor.
        /// </summary>
        public ShellPage()
        {
            InitializeComponent();

            DataContext = ViewModel;
            ShellHandler = this;
            ViewModel.Initialize(shellFrame, navigationView, KeyboardAccelerators);

            // NL moved navigation to general page to the moment when the window is first activated (to not make flyout window disappear)
            // shellFrame.Navigate(typeof(GeneralPage));
            IPCResponseHandleList.Add(ReceiveMessage);
            SetTitleBar();
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

        private void OobeButton_Click(object sender, RoutedEventArgs e)
        {
            OpenOobeWindowCallback();
        }

        private bool navigationViewInitialStateProcessed; // avoid announcing initial state of the navigation pane.

        private void NavigationView_PaneOpened(Microsoft.UI.Xaml.Controls.NavigationView sender, object args)
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
                var loader = Helpers.ResourceLoaderInstance.ResourceLoader;
                peer.RaiseNotificationEvent(
                    AutomationNotificationKind.ActionCompleted,
                    AutomationNotificationProcessing.ImportantMostRecent,
                    loader.GetString("Shell_NavigationMenu_Announce_Open"),
                    "navigationMenuPaneOpened");
            }
        }

        private void NavigationView_PaneClosed(Microsoft.UI.Xaml.Controls.NavigationView sender, object args)
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
                var loader = Helpers.ResourceLoaderInstance.ResourceLoader;
                peer.RaiseNotificationEvent(
                    AutomationNotificationKind.ActionCompleted,
                    AutomationNotificationProcessing.ImportantMostRecent,
                    loader.GetString("Shell_NavigationMenu_Announce_Collapse"),
                    "navigationMenuPaneClosed");
            }
        }

        private void OOBEItem_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            OpenOobeWindowCallback();
        }

        private async void FeedbackItem_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://aka.ms/powerToysGiveFeedback"));
        }

        private void WhatIsNewItem_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            OpenWhatIsNewWindowCallback();
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            NavigationViewItem selectedItem = args.SelectedItem as NavigationViewItem;
            if (selectedItem != null)
            {
                Type pageType = selectedItem.GetValue(NavHelper.NavigateToProperty) as Type;
                NavigationService.Navigate(pageType);
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

        private void SetTitleBar()
        {
            var u = App.GetSettingsWindow();
            if (u != null)
            {
                // A custom title bar is required for full window theme and Mica support.
                // https://docs.microsoft.com/windows/apps/develop/title-bar?tabs=winui3#full-customization
                u.ExtendsContentIntoTitleBar = true;
                u.SetTitleBar(AppTitleBar);
                var loader = ResourceLoaderInstance.ResourceLoader;
                AppTitleBarText.Text = App.IsElevated ? loader.GetString("SettingsWindow_AdminTitle") : loader.GetString("SettingsWindow_Title");
#if DEBUG
                DebugMessage.Visibility = Visibility.Visible;
#endif
            }
        }

        private void ShellPage_Loaded(object sender, RoutedEventArgs e)
        {
            SetTitleBar();
        }

        private void NavigationView_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
        {
            if (args.DisplayMode == NavigationViewDisplayMode.Compact || args.DisplayMode == NavigationViewDisplayMode.Minimal)
            {
                PaneToggleBtn.Visibility = Visibility.Visible;
                AppTitleBar.Margin = new Thickness(48, 0, 0, 0);
                AppTitleBarText.Margin = new Thickness(12, 0, 0, 0);
            }
            else
            {
                PaneToggleBtn.Visibility = Visibility.Collapsed;
                AppTitleBar.Margin = new Thickness(16, 0, 0, 0);
                AppTitleBarText.Margin = new Thickness(16, 0, 0, 0);
            }
        }

        private void PaneToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            navigationView.IsPaneOpen = !navigationView.IsPaneOpen;
        }
    }
}
