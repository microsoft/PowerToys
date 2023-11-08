// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.PowerLauncher.Telemetry;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Data.Json;
using WinUIEx;

namespace Microsoft.PowerToys.Settings.UI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : WindowEx
    {
        public MainWindow(bool isDark, bool createHidden = false)
        {
            var bootTime = new System.Diagnostics.Stopwatch();
            bootTime.Start();

            ShellPage.SetElevationStatus(App.IsElevated);
            ShellPage.SetIsUserAnAdmin(App.IsUserAnAdmin);

            // Set window icon
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.SetIcon("Assets\\Settings\\icon.ico");

            // Passed by parameter, as it needs to be evaluated ASAP, otherwise there is a white flash
            if (isDark)
            {
                ThemeHelpers.SetImmersiveDarkMode(hWnd, isDark);
            }

            var placement = Utils.DeserializePlacementOrDefault(hWnd);
            if (createHidden)
            {
                placement.ShowCmd = NativeMethods.SW_HIDE;
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
                    App.SetOobeWindow(new OobeWindow(Microsoft.PowerToys.Settings.UI.OOBE.Enums.PowerToysModules.Overview, App.IsDarkTheme()));
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

            SetTheme(isDark);

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
            Utils.SerializePlacement(hWnd);

            if (App.GetOobeWindow() == null)
            {
                App.ClearSettingsWindow();
            }
            else
            {
                args.Handled = true;
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_HIDE);
            }
        }

        internal void EnsurePageIsSelected()
        {
            ShellPage.EnsurePageIsSelected();
        }

        private void SetTheme(bool isDark)
        {
            shellPage.RequestedTheme = isDark ? ElementTheme.Dark : ElementTheme.Light;
        }
    }
}
