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
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
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

            // Initialize UI components immediately for faster visual feedback
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            SetAppTitleBar();

            // Set up critical event handlers
            this.Activated += Window_Activated_SetIcon;
            App.ThemeService.ThemeChanged += OnThemeChanged;

            // Set elevation status immediately (required for UI)
            ShellPage.SetElevationStatus(App.IsElevated);
            ShellPage.SetIsUserAnAdmin(App.IsUserAnAdmin);

            // Apply theme immediately
            App.ThemeService.ApplyTheme();

            // Set window title immediately
            var loader = ResourceLoaderInstance.ResourceLoader;
            Title = App.IsElevated ? loader.GetString("SettingsWindow_AdminTitle") : loader.GetString("SettingsWindow_Title");

            // Handle window visibility
            var hWnd = WindowNative.GetWindowHandle(this);
            if (createHidden)
            {
                var placement = new WINDOWPLACEMENT
                {
                    ShowCmd = NativeMethods.SW_HIDE,
                };
                NativeMethods.SetWindowPlacement(hWnd, ref placement);

                // Restore the last known placement on the first activation
                this.Activated += Window_Activated;
            }

            // Initialize remaining components asynchronously
            _ = InitializeAsync(hWnd, createHidden, bootTime);
        }

        private async Task InitializeAsync(IntPtr hWnd, bool createHidden, System.Diagnostics.Stopwatch bootTime)
        {
            try
            {
                // Load window placement asynchronously (non-blocking file I/O)
                if (!createHidden)
                {
                    await Task.Run(() =>
                    {
                        var placement = WindowHelper.DeserializePlacementOrDefault(hWnd);
                        NativeMethods.SetWindowPlacement(hWnd, ref placement);
                    });
                }

                // Set up IPC callbacks on UI thread
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    // send IPC Message
                    ShellPage.SetDefaultSndMessageCallback(msg =>
                    {
                        // Use SendIPCMessage which handles queuing if IPC is not yet initialized
                        App.SendIPCMessage(msg);
                    });

                    // send IPC Message
                    ShellPage.SetRestartAdminSndMessageCallback(msg =>
                    {
                        App.SendIPCMessage(msg);
                        Environment.Exit(0); // close application
                    });

                    // send IPC Message
                    ShellPage.SetCheckForUpdatesMessageCallback(msg =>
                    {
                        App.SendIPCMessage(msg);
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

                    // open oobe
                    ShellPage.SetOpenOobeCallback(() =>
                    {
                        if (App.GetOobeWindow() == null)
                        {
                            App.SetOobeWindow(new OobeWindow(OOBE.Enums.PowerToysModules.Overview));
                        }

                        App.GetOobeWindow().Activate();
                    });

                    // open whats new window
                    ShellPage.SetOpenWhatIsNewCallback(() =>
                    {
                        if (App.GetScoobeWindow() == null)
                        {
                            App.SetScoobeWindow(new ScoobeWindow());
                        }

                        App.GetScoobeWindow().Activate();
                    });

                    // receive IPC Message
                    App.IPCMessageReceivedCallback = (string msg) =>
                    {
                        // Ignore empty or whitespace-only messages
                        if (string.IsNullOrWhiteSpace(msg))
                        {
                            return;
                        }

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
                                // Log with message preview for debugging (limit to 100 chars to avoid log spam)
                                var msgPreview = msg.Length > 100 ? string.Concat(msg.AsSpan(0, 100), "...") : msg;
                                Logger.LogError($"Failed to parse JSON from IPC message. Message preview: {msgPreview}");
                            }
                        }
                    };
                });

                // Record telemetry asynchronously
                bootTime.Stop();
                await Task.Run(() =>
                {
                    PowerToysTelemetry.Log.WriteEvent(new SettingsBootEvent() { BootTimeMs = bootTime.ElapsedMilliseconds });
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error during async initialization: {ex.Message}");
            }
        }

        private void SetAppTitleBar()
        {
            // We need to assign the window here so it can configure the custom title bar area correctly.
            shellPage.TitleBar.Window = this;
            WindowHelpers.ForceTopBorder1PixelInsetOnWindows10(WindowNative.GetWindowHandle(this));
        }

        public void NavigateToSection(System.Type type)
        {
            ShellPage.Navigate(type);
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

            if (App.GetOobeWindow() == null && App.GetScoobeWindow() == null)
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
    }
}
