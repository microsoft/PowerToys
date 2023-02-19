// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.PowerLauncher.Telemetry;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.Resources;
using Windows.Data.Json;
using WinUIEx;

namespace Microsoft.PowerToys.Settings.UI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
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
            appWindow.SetIcon("icon.ico");

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

            ResourceLoader loader = ResourceLoader.GetForViewIndependentUse();
            Title = loader.GetString("SettingsWindow_Title");

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
            ShellPage.SetOpenMainWindowCallback(() =>
            {
                this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                     App.OpenSettingsWindow(typeof(GeneralPage)));
            });

            // open main window
            ShellPage.SetUpdatingGeneralSettingsCallback((string module, bool isEnabled) =>
            {
                SettingsRepository<GeneralSettings> repository = SettingsRepository<GeneralSettings>.GetInstance(new SettingsUtils());
                GeneralSettings generalSettingsConfig = repository.SettingsConfig;
                bool needToUpdate = false;
                switch (module)
                {
                    case "AlwaysOnTop":
                        needToUpdate = generalSettingsConfig.Enabled.AlwaysOnTop != isEnabled;
                        generalSettingsConfig.Enabled.AlwaysOnTop = isEnabled; break;
                    case "Awake":
                        needToUpdate = generalSettingsConfig.Enabled.Awake != isEnabled;
                        generalSettingsConfig.Enabled.Awake = isEnabled; break;
                    case "ColorPicker":
                        needToUpdate = generalSettingsConfig.Enabled.ColorPicker != isEnabled;
                        generalSettingsConfig.Enabled.ColorPicker = isEnabled; break;
                    case "FancyZones":
                        needToUpdate = generalSettingsConfig.Enabled.FancyZones != isEnabled;
                        generalSettingsConfig.Enabled.FancyZones = isEnabled; break;
                    case "FileLocksmith":
                        needToUpdate = generalSettingsConfig.Enabled.FileLocksmith != isEnabled;
                        generalSettingsConfig.Enabled.FileLocksmith = isEnabled; break;
                    case "FindMyMouse":
                        needToUpdate = generalSettingsConfig.Enabled.FindMyMouse != isEnabled;
                        generalSettingsConfig.Enabled.FindMyMouse = isEnabled; break;
                    case "Hosts":
                        needToUpdate = generalSettingsConfig.Enabled.Hosts != isEnabled;
                        generalSettingsConfig.Enabled.Hosts = isEnabled; break;
                    case "ImageResizer":
                        needToUpdate = generalSettingsConfig.Enabled.ImageResizer != isEnabled;
                        generalSettingsConfig.Enabled.ImageResizer = isEnabled; break;
                    case "KeyboardManager":
                        needToUpdate = generalSettingsConfig.Enabled.KeyboardManager != isEnabled;
                        generalSettingsConfig.Enabled.KeyboardManager = isEnabled; break;
                    case "MouseHighlighter":
                        needToUpdate = generalSettingsConfig.Enabled.MouseHighlighter != isEnabled;
                        generalSettingsConfig.Enabled.MouseHighlighter = isEnabled; break;
                    case "MousePointerCrosshairs":
                        needToUpdate = generalSettingsConfig.Enabled.MousePointerCrosshairs != isEnabled;
                        generalSettingsConfig.Enabled.MousePointerCrosshairs = isEnabled; break;
                    case "PowerRename":
                        needToUpdate = generalSettingsConfig.Enabled.PowerRename != isEnabled;
                        generalSettingsConfig.Enabled.PowerRename = isEnabled; break;
                    case "PowerLauncher":
                        needToUpdate = generalSettingsConfig.Enabled.PowerLauncher != isEnabled;
                        generalSettingsConfig.Enabled.PowerLauncher = isEnabled; break;
                    case "PowerAccent":
                        needToUpdate = generalSettingsConfig.Enabled.PowerAccent != isEnabled;
                        generalSettingsConfig.Enabled.PowerAccent = isEnabled; break;
                    case "MeasureTool":
                        needToUpdate = generalSettingsConfig.Enabled.MeasureTool != isEnabled;
                        generalSettingsConfig.Enabled.MeasureTool = isEnabled; break;
                    case "ShortcutGuide":
                        needToUpdate = generalSettingsConfig.Enabled.ShortcutGuide != isEnabled;
                        generalSettingsConfig.Enabled.ShortcutGuide = isEnabled; break;
                    case "PowerOCR":
                        needToUpdate = generalSettingsConfig.Enabled.PowerOCR != isEnabled;
                        generalSettingsConfig.Enabled.PowerOCR = isEnabled; break;
                    case "VideoConference":
                        needToUpdate = generalSettingsConfig.Enabled.VideoConference != isEnabled;
                        generalSettingsConfig.Enabled.VideoConference = isEnabled; break;
                }

                if (needToUpdate)
                {
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
                    Utils.BecomeForegroundWindow(flyout.GetWindowHandle());
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
    }
}
