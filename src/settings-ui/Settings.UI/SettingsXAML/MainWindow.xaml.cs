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
using Windows.Data.Json;
using WinRT.Interop;
using WinUIEx;

namespace Microsoft.PowerToys.Settings.UI
{
    public sealed partial class MainWindow : WindowEx
    {
        private const bool WaitForInitialContentBeforeActivation = true;

        private DispatcherQueueTimer _activationFallbackTimer;
        private bool _bringToForegroundOnActivation;
        private bool _activationPending;
        private bool _closed;

        public MainWindow()
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
            placement.ShowCmd = NativeMethods.SW_HIDE;

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
            // We need to assign the window here so it can configure the custom title bar area correctly.
            shellPage.TitleBar.Window = this;
            this.ExtendsContentIntoTitleBar = true;
            WindowHelpers.ForceTopBorder1PixelInsetOnWindows10(WindowNative.GetWindowHandle(this));
        }

        public void NavigateToSection(Type type)
        {
            ShellPage.Navigate(type);
        }

        public void ActivateWhenReady(bool bringToForeground = false)
        {
            _bringToForegroundOnActivation |= bringToForeground;

            var hWnd = WindowNative.GetWindowHandle(this);
            if (!WaitForInitialContentBeforeActivation || NativeMethods.IsWindowVisible(hWnd) || shellPage.IsInitialContentLoaded)
            {
                ActivatePreparedWindow();
                return;
            }

            shellPage.InitialContentLoaded -= ShellPage_InitialContentLoaded;
            shellPage.InitialContentLoaded += ShellPage_InitialContentLoaded;
            _activationPending = true;

            _activationFallbackTimer ??= DispatcherQueue.CreateTimer();
            if (!_activationFallbackTimer.IsRunning)
            {
                _activationFallbackTimer.Interval = TimeSpan.FromSeconds(2);
                _activationFallbackTimer.IsRepeating = false;
                _activationFallbackTimer.Tick -= ActivationFallbackTimer_Tick;
                _activationFallbackTimer.Tick += ActivationFallbackTimer_Tick;
                _activationFallbackTimer.Start();
            }
        }

        public void BeginWindowSession()
        {
            shellPage.BeginWindowSession();
        }

        public void CloseHiddenWindow()
        {
            var hWnd = WindowNative.GetWindowHandle(this);
            if (!NativeMethods.IsWindowVisible(hWnd) && !_activationPending)
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
                _closed = true;
                _activationPending = false;
                shellPage.Dispose();
                App.ClearSettingsWindow();

                shellPage.InitialContentLoaded -= ShellPage_InitialContentLoaded;
                _activationFallbackTimer?.Stop();
                App.ThemeService.ThemeChanged -= OnThemeChanged;
            }
            else
            {
                args.Handled = true;
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_HIDE);
            }
        }

        private void ShellPage_InitialContentLoaded(object sender, EventArgs e)
        {
            if (_closed)
            {
                return;
            }

            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, ActivatePreparedWindow);
        }

        private void ActivationFallbackTimer_Tick(DispatcherQueueTimer sender, object args)
        {
            if (_closed)
            {
                return;
            }

            ActivatePreparedWindow();
        }

        private void ActivatePreparedWindow()
        {
            if (_closed)
            {
                return;
            }

            shellPage.InitialContentLoaded -= ShellPage_InitialContentLoaded;
            _activationFallbackTimer?.Stop();
            _activationPending = false;

            var hWnd = WindowNative.GetWindowHandle(this);
            if (!NativeMethods.IsWindowVisible(hWnd))
            {
                var placement = WindowHelper.DeserializePlacementOrDefault(hWnd);
                NativeMethods.SetWindowPlacement(hWnd, ref placement);
            }

            Activate();
            if (_bringToForegroundOnActivation)
            {
                _bringToForegroundOnActivation = false;

                // https://github.com/microsoft/microsoft-ui-xaml/issues/7595 - Activate doesn't bring window to the foreground
                WindowHelpers.BringToForeground(hWnd);
            }
        }

        private void Window_Activated_SetIcon(object sender, WindowActivatedEventArgs args)
        {
            // Set window icon
            this.SetIcon("Assets\\Settings\\icon.ico");
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
