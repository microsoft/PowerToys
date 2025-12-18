// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using PowerDisplay.Common;
using PowerDisplay.Helpers;
using PowerDisplay.Serialization;
using PowerToys.Interop;

namespace PowerDisplay
{
    /// <summary>
    /// PowerDisplay application main class
    /// </summary>
#pragma warning disable CA1001 // CancellationTokenSource is disposed in Shutdown/ForceExit methods
    public partial class App : Application
#pragma warning restore CA1001
    {
        private readonly ISettingsUtils _settingsUtils = SettingsUtils.Default;
        private Window? _mainWindow;
        private int _powerToysRunnerPid;
        private TrayIconService? _trayIconService;

        public App(int runnerPid)
        {
            Logger.LogInfo($"App constructor: Starting with runnerPid={runnerPid}");
            _powerToysRunnerPid = runnerPid;

            Logger.LogTrace("App constructor: Calling InitializeComponent");
            this.InitializeComponent();

            // Ensure types used in XAML are preserved for AOT compilation
            TypePreservation.PreserveTypes();

            // Note: Logger is already initialized in Program.cs before App constructor
            Logger.LogTrace("App constructor: InitializeComponent completed");

            // Initialize PowerToys telemetry
            try
            {
                PowerToysTelemetry.Log.WriteEvent(new Telemetry.Events.PowerDisplayStartEvent());
                Logger.LogTrace("App constructor: Telemetry event sent");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"App constructor: Telemetry failed: {ex.Message}");
            }

            // Initialize language settings
            string appLanguage = LanguageHelper.LoadLanguage();
            if (!string.IsNullOrEmpty(appLanguage))
            {
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = appLanguage;
                Logger.LogTrace($"App constructor: Language set to {appLanguage}");
            }

            // Handle unhandled exceptions
            this.UnhandledException += OnUnhandledException;
            Logger.LogInfo("App constructor: Completed");
        }

        /// <summary>
        /// Handle unhandled exceptions
        /// </summary>
        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logger.LogError("Unhandled exception", e.Exception);
        }

        /// <summary>
        /// Called when the application is launched
        /// </summary>
        /// <param name="args">Launch arguments</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            Logger.LogInfo("OnLaunched: Application launching");
            try
            {
                // Single instance is already ensured by AppInstance.FindOrRegisterForKey() in Program.cs
                // PID is already parsed in Program.cs and passed to constructor

                // Set up Windows Events monitoring (Awake pattern)
                // Note: PowerDisplay.exe should NOT listen to RefreshMonitorsEvent
                // That event is sent BY PowerDisplay TO Settings UI for one-way notification
                Logger.LogInfo("OnLaunched: Registering Windows Events for IPC...");
                RegisterWindowEvent(Constants.ShowPowerDisplayEvent(), mw => mw.ShowWindow(), "Show");
                Logger.LogTrace($"OnLaunched: Registered Show event: {Constants.ShowPowerDisplayEvent()}");
                RegisterWindowEvent(Constants.TogglePowerDisplayEvent(), mw => mw.ToggleWindow(), "Toggle");
                Logger.LogTrace($"OnLaunched: Registered Toggle event: {Constants.TogglePowerDisplayEvent()}");
                RegisterEvent(Constants.TerminatePowerDisplayEvent(), () => Environment.Exit(0), "Terminate");
                Logger.LogTrace($"OnLaunched: Registered Terminate event: {Constants.TerminatePowerDisplayEvent()}");
                RegisterViewModelEvent(
                    Constants.SettingsUpdatedPowerDisplayEvent(),
                    vm =>
                    {
                        vm.ApplySettingsFromUI();

                        // Refresh tray icon based on updated settings
                        _trayIconService?.SetupTrayIcon();
                    },
                    "SettingsUpdated");
                RegisterViewModelEvent(Constants.ApplyColorTemperaturePowerDisplayEvent(), vm => vm.ApplyColorTemperatureFromSettings(), "ApplyColorTemperature");
                RegisterViewModelEvent(Constants.ApplyProfilePowerDisplayEvent(), vm => vm.ApplyProfileFromSettings(), "ApplyProfile");
                RegisterViewModelEvent(Constants.PowerDisplaySendSettingsTelemetryEvent(), vm => vm.SendSettingsTelemetry(), "SendSettingsTelemetry");

                // LightSwitch integration - apply profiles when theme changes
                RegisterViewModelEvent(PathConstants.LightSwitchLightThemeEventName, vm => vm.ApplyLightSwitchProfile(isLightMode: true), "LightSwitch-Light");
                RegisterViewModelEvent(PathConstants.LightSwitchDarkThemeEventName, vm => vm.ApplyLightSwitchProfile(isLightMode: false), "LightSwitch-Dark");
                Logger.LogInfo("OnLaunched: All Windows Events registered");

                // Monitor Runner process (backup exit mechanism)
                if (_powerToysRunnerPid > 0)
                {
                    Logger.LogInfo($"OnLaunched: PowerDisplay started from PowerToys Runner. Runner pid={_powerToysRunnerPid}");

                    RunnerHelper.WaitForPowerToysRunner(_powerToysRunnerPid, () =>
                    {
                        Logger.LogInfo("OnLaunched: PowerToys Runner exited. Exiting PowerDisplay");
                        Environment.Exit(0);
                    });
                }
                else
                {
                    Logger.LogInfo("OnLaunched: PowerDisplay started in standalone mode (no runner PID)");
                }

                // Create main window
                Logger.LogInfo("OnLaunched: Creating MainWindow");
                _mainWindow = new MainWindow();
                Logger.LogInfo("OnLaunched: MainWindow created");

                // Initialize tray icon service
                Logger.LogTrace("OnLaunched: Initializing TrayIconService");
                _trayIconService = new TrayIconService(
                    _settingsUtils,
                    ToggleMainWindow,
                    () => Environment.Exit(0),
                    OpenSettings);
                _trayIconService.SetupTrayIcon();
                Logger.LogTrace("OnLaunched: TrayIconService initialized");

                // Window visibility depends on launch mode
                bool isStandaloneMode = _powerToysRunnerPid <= 0;
                Logger.LogInfo($"OnLaunched: isStandaloneMode={isStandaloneMode}");

                if (isStandaloneMode)
                {
                    // Standalone mode - activate and show window immediately
                    Logger.LogInfo("OnLaunched: Activating window (standalone mode)");
                    _mainWindow.Activate();
                    Logger.LogInfo("OnLaunched: Window activated (standalone mode)");
                }
                else
                {
                    // PowerToys mode - window remains hidden until show event received
                    // Background initialization runs automatically via MainWindow constructor
                    Logger.LogInfo("OnLaunched: Window created but hidden, waiting for show/toggle event (PowerToys mode)");
                }

                Logger.LogInfo("OnLaunched: Application launch completed");
            }
            catch (Exception ex)
            {
                Logger.LogError($"OnLaunched: PowerDisplay startup failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Register a simple event handler (no window access needed)
        /// </summary>
        private void RegisterEvent(string eventName, Action action, string logName)
        {
            Logger.LogTrace($"RegisterEvent: Setting up event listener for '{logName}' on event '{eventName}'");
            NativeEventWaiter.WaitForEventLoop(
                eventName,
                () =>
                {
                    Logger.LogInfo($"[EVENT] {logName} event received from event '{eventName}'");
                    try
                    {
                        action();
                        Logger.LogTrace($"[EVENT] {logName} action completed");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"[EVENT] {logName} action failed: {ex.Message}");
                    }
                },
                CancellationToken.None);
        }

        /// <summary>
        /// Register an event handler that operates on MainWindow directly
        /// NativeEventWaiter already marshals to UI thread
        /// </summary>
        private void RegisterWindowEvent(string eventName, Action<MainWindow> action, string logName)
        {
            Logger.LogTrace($"RegisterWindowEvent: Setting up window event listener for '{logName}' on event '{eventName}'");
            NativeEventWaiter.WaitForEventLoop(
                eventName,
                () =>
                {
                    Logger.LogInfo($"[EVENT] {logName} window event received from event '{eventName}'");
                    if (_mainWindow is MainWindow mainWindow)
                    {
                        Logger.LogTrace($"[EVENT] {logName}: MainWindow is valid, invoking action");
                        try
                        {
                            action(mainWindow);
                            Logger.LogTrace($"[EVENT] {logName}: Window action completed");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"[EVENT] {logName}: Window action failed: {ex.Message}");
                        }
                    }
                    else
                    {
                        Logger.LogError($"[EVENT] {logName}: _mainWindow is null or not MainWindow type");
                    }
                },
                CancellationToken.None);
        }

        /// <summary>
        /// Register an event handler that operates on ViewModel via DispatcherQueue
        /// Used for Settings UI IPC events that need ViewModel access
        /// </summary>
        private void RegisterViewModelEvent(string eventName, Action<ViewModels.MainViewModel> action, string logName)
        {
            NativeEventWaiter.WaitForEventLoop(
                eventName,
                () =>
                {
                    Logger.LogInfo($"[EVENT] {logName} event received");
                    _mainWindow?.DispatcherQueue.TryEnqueue(() =>
                    {
                        if (_mainWindow is MainWindow mainWindow && mainWindow.ViewModel != null)
                        {
                            action(mainWindow.ViewModel);
                        }
                    });
                },
                CancellationToken.None);
        }

        /// <summary>
        /// Gets the main window instance
        /// </summary>
        public Window? MainWindow => _mainWindow;

        /// <summary>
        /// Show the main window
        /// </summary>
        private void ShowMainWindow()
        {
            Logger.LogInfo("ShowMainWindow: Called");
            if (_mainWindow is MainWindow mainWindow)
            {
                Logger.LogTrace("ShowMainWindow: MainWindow is valid, calling ShowWindow");
                mainWindow.ShowWindow();
            }
            else
            {
                Logger.LogError("ShowMainWindow: _mainWindow is null or not MainWindow type");
            }
        }

        /// <summary>
        /// Toggle the main window visibility
        /// </summary>
        private void ToggleMainWindow()
        {
            Logger.LogInfo("ToggleMainWindow: Called");
            if (_mainWindow is MainWindow mainWindow)
            {
                Logger.LogTrace($"ToggleMainWindow: MainWindow is valid, current visibility={mainWindow.IsWindowVisible()}");
                mainWindow.ToggleWindow();
            }
            else
            {
                Logger.LogError("ToggleMainWindow: _mainWindow is null or not MainWindow type");
            }
        }

        /// <summary>
        /// Open PowerDisplay settings in PowerToys Settings UI
        /// </summary>
        private void OpenSettings()
        {
            // mainExecutableIsOnTheParentFolder = true because PowerDisplay is a WinUI 3 app
            // deployed in a subfolder (PowerDisplay\) while PowerToys.exe is in the parent folder
            SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.PowerDisplay, true);
        }

        /// <summary>
        /// Refresh tray icon based on current settings
        /// </summary>
        public void RefreshTrayIcon()
        {
            _trayIconService?.SetupTrayIcon();
        }

        /// <summary>
        /// Check if running standalone (not launched from PowerToys Runner)
        /// </summary>
        public bool IsRunningDetachedFromPowerToys()
        {
            return _powerToysRunnerPid == -1;
        }

        /// <summary>
        /// Shutdown application (Awake pattern - simple and clean)
        /// </summary>
        public void Shutdown()
        {
            Logger.LogInfo("PowerDisplay shutting down");
            _trayIconService?.Destroy();
            Environment.Exit(0);
        }
    }
}
