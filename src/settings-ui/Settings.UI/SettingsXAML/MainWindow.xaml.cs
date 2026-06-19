// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerLauncher.Telemetry;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Data.Json;
using WinRT.Interop;
using WinUIEx;

namespace Microsoft.PowerToys.Settings.UI
{
    public sealed partial class MainWindow : WindowEx
    {
        public MainWindow(bool createHidden = false)
        {
            var bootTime = new System.Diagnostics.Stopwatch();
            bootTime.Start();

            this.Activated += Window_Activated_SetIcon;

            App.ThemeService.ThemeChanged += OnThemeChanged;
            App.ThemeService.ApplyTheme();

            this.ExtendsContentIntoTitleBar = true;

            ShellPage.SetElevationStatus(App.IsElevated);
            ShellPage.SetIsUserAnAdmin(App.IsUserAnAdmin);

            var hWnd = WindowNative.GetWindowHandle(this);
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
                DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
                     App.OpenSettingsWindow(type));
            });

            // open main window
            ShellPage.SetUpdatingGeneralSettingsCallback((ModuleType moduleType, bool isEnabled) =>
            {
                SettingsRepository<GeneralSettings> repository = SettingsRepository<GeneralSettings>.GetInstance(SettingsUtils.Default);
                GeneralSettings generalSettingsConfig = repository.SettingsConfig;
                bool needToUpdate = ModuleHelper.GetIsModuleEnabled(generalSettingsConfig, moduleType) != isEnabled;

                if (needToUpdate)
                {
                    ModuleHelper.SetIsModuleEnabled(generalSettingsConfig, moduleType, isEnabled);
                    var outgoing = new OutGoingGeneralSettings(generalSettingsConfig);

                    // Save settings to file
                    SettingsUtils.Default.SaveSettings(generalSettingsConfig.ToJsonString());

                    // Send IPC message asynchronously to avoid blocking UI and potential recursive calls
                    Task.Run(() =>
                    {
                        ShellPage.SendDefaultIPCMessage(outgoing.ToString());
                    });

                    ShellPage.ShellHandler?.SignalGeneralDataUpdate();
                }

                return needToUpdate;
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

        private void SetTitleBar()
        {
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);
            this.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            WindowHelpers.ForceTopBorder1PixelInsetOnWindows10(WindowNative.GetWindowHandle(this));

            // Set the title based on elevation state.
            var loader = ResourceLoaderInstance.ResourceLoader;
            AppTitleBar.Title = App.IsElevated ? loader.GetString("SettingsWindow_AdminTitle") : loader.GetString("SettingsWindow_Title");
#if DEBUG
            AppTitleBar.Subtitle = "Debug";
#endif

            // Give ShellPage a reference to the SearchBox (defined here in XAML
            // so drag regions are computed correctly by the built-in TitleBar).
            shellPage.SearchBox = SearchBox;

            // The built-in TitleBar's drag region auto-computation is unreliable
            // in this setup. Manually register the SearchBox as a passthrough
            // (non-draggable) region via InputNonClientPointerSource.
            SearchBox.SizeChanged += (_, _) => UpdateSearchBoxPassthroughRegion();

            // Update TitleBar pane toggle visibility when NavigationView display mode changes.
            shellPage.DisplayModeChanged = mode =>
            {
                AppTitleBar.IsPaneToggleButtonVisible =
                    mode == NavigationViewDisplayMode.Compact ||
                    mode == NavigationViewDisplayMode.Minimal;
            };

            // Caption button theming — the built-in TitleBar handles drag regions
            // and layout automatically, but caption button foreground colors need
            // to be set explicitly for theme changes (WinUI known issue).
            if (this.Content is FrameworkElement rootElement)
            {
                ApplyThemeToCaptionButtons(rootElement.ActualTheme);
                rootElement.ActualThemeChanged += (_, _) => ApplyThemeToCaptionButtons(rootElement.ActualTheme);
            }
        }

        private void ApplyThemeToCaptionButtons(ElementTheme theme)
        {
            this.AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            this.AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            if (theme == ElementTheme.Dark)
            {
                this.AppWindow.TitleBar.ButtonForegroundColor = Colors.White;
                this.AppWindow.TitleBar.ButtonInactiveForegroundColor = Colors.DarkGray;
            }
            else
            {
                this.AppWindow.TitleBar.ButtonForegroundColor = Colors.Black;
                this.AppWindow.TitleBar.ButtonInactiveForegroundColor = Colors.DarkGray;
            }
        }

        public void NavigateToSection(Type type)
        {
            ShellPage.Navigate(type);
        }

        private void UpdateSearchBoxPassthroughRegion()
        {
            if (SearchBox.ActualWidth == 0)
            {
                return;
            }

            var scale = SearchBox.XamlRoot.RasterizationScale;
            var transform = SearchBox.TransformToVisual(null);
            var bounds = transform.TransformBounds(
                new global::Windows.Foundation.Rect(0, 0, SearchBox.ActualWidth, SearchBox.ActualHeight));

            var rect = new global::Windows.Graphics.RectInt32(
                _X: (int)Math.Round(bounds.X * scale),
                _Y: (int)Math.Round(bounds.Y * scale),
                _Width: (int)Math.Round(bounds.Width * scale),
                _Height: (int)Math.Round(bounds.Height * scale));

            var source = Microsoft.UI.Input.InputNonClientPointerSource.GetForWindowId(this.AppWindow.Id);
            source.SetRegionRects(Microsoft.UI.Input.NonClientRegionKind.Passthrough, new[] { rect });
        }

        private void TitleBar_PaneToggleRequested(Microsoft.UI.Xaml.Controls.TitleBar sender, object args)
        {
            shellPage.ToggleNavigationPane();
        }

        public void CloseHiddenWindow()
        {
            var hWnd = WindowNative.GetWindowHandle(this);
            if (!NativeMethods.IsWindowVisible(hWnd))
            {
                Close();
            }
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            var hWnd = WindowNative.GetWindowHandle(this);
            WindowHelper.SerializePlacement(hWnd);

            if (!App.IsSecondaryWindowOpen())
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
            this.SetIcon("Assets\\Settings\\icon.ico");
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState != WindowActivationState.Deactivated)
            {
                this.Activated -= Window_Activated;
                var hWnd = WindowNative.GetWindowHandle(this);
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

        // Search event forwarders — the SearchBox lives in MainWindow's XAML
        // (required for TitleBar drag regions) but the search logic is in ShellPage.
        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
            => shellPage.HandleSearchTextChanged(sender, args);

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
            => shellPage.HandleSearchQuerySubmitted(sender, args);

        private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
            => shellPage.HandleSearchSuggestionChosen(sender, args);

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
            => shellPage.HandleSearchGotFocus(sender, e);

        private void CtrlF_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
            => shellPage.HandleCtrlF(sender, args);
    }
}
