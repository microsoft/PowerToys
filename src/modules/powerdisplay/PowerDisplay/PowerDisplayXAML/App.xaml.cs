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
    public partial class App : Application
    {
        private readonly SettingsUtils _settingsUtils = SettingsUtils.Default;
        private Window? _mainWindow;
        private int _powerToysRunnerPid;
        private string? _pipeName;
        private TrayIconService? _trayIconService;

        public App(int runnerPid, string? pipeName)
        {
            Logger.LogInfo($"App constructor: Starting with runnerPid={runnerPid}, pipeName={pipeName ?? "null"}");
            _powerToysRunnerPid = runnerPid;
            _pipeName = pipeName;

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
                RegisterWindowEvent(Constants.TogglePowerDisplayEvent(), mw => mw.ToggleWindow(), "Toggle");
                Logger.LogTrace($"OnLaunched: Registered Toggle event: {Constants.TogglePowerDisplayEvent()}");
                RegisterEvent(Constants.TerminatePowerDisplayEvent(), () => Environment.Exit(0), "Terminate");
                Logger.LogTrace($"OnLaunched: Registered Terminate event: {Constants.TerminatePowerDisplayEvent()}");
                RegisterWindowEvent(
                    Constants.SettingsUpdatedPowerDisplayEvent(),
                    mw =>
                    {
                        mw.ViewModel.ApplySettingsFromUI();

                        // Refresh tray icon based on updated settings
                        _trayIconService?.SetupTrayIcon();
                    },
                    "SettingsUpdated");
                RegisterWindowEvent(
                    Constants.HotkeyUpdatedPowerDisplayEvent(),
                    mw => mw.ReloadHotkeySettings(),
                    "HotkeyUpdated");
                RegisterViewModelEvent(Constants.PowerDisplaySendSettingsTelemetryEvent(), vm => vm.SendSettingsTelemetry(), "SendSettingsTelemetry");

                // LightSwitch integration - apply profiles when theme changes
                RegisterViewModelEvent(PathConstants.LightSwitchLightThemeEventName, vm => vm.ApplyLightSwitchProfile(isLightMode: true), "LightSwitch-Light");
                RegisterViewModelEvent(PathConstants.LightSwitchDarkThemeEventName, vm => vm.ApplyLightSwitchProfile(isLightMode: false), "LightSwitch-Dark");
                Logger.LogInfo("OnLaunched: All Windows Events registered");

                // Connect to Named Pipe for IPC with module DLL (if pipe name provided)
                if (!string.IsNullOrEmpty(_pipeName))
                {
                    Logger.LogInfo($"OnLaunched: Starting Named Pipe processing for pipe: {_pipeName}");
                    ProcessNamedPipe(_pipeName);
                }
                else
                {
                    Logger.LogInfo("OnLaunched: No pipe name provided, skipping Named Pipe setup");
                }

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
            SettingsDeepLink.OpenSettings(true);
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

        /// <summary>
        /// Connect to Named Pipe and process messages from module DLL
        /// </summary>
        private void ProcessNamedPipe(string pipeName)
        {
            void OnMessage(string message) => _mainWindow?.DispatcherQueue.TryEnqueue(async () => await OnNamedPipeMessage(message));

            TwoWayPipeMessageIPCManaged ipc = new(
                $"\\\\.\\pipe\\{pipeName}",
                "\\\\.\\pipe\\powertoys_power_display_input",
                OnMessage);
            ipc.Start();
        }

        /// <summary>
        /// Handle messages received from the module DLL via Named Pipe
        /// </summary>
        private async Task OnNamedPipeMessage(string message)
        {
            var messageParts = message.Split(' ', 2);
            var messageType = messageParts[0];

            Logger.LogInfo($"[NamedPipe] Processing message type: {messageType}");

            if (messageType == Constants.PowerDisplayToggleMessage())
            {
                // Toggle window visibility
                if (_mainWindow is MainWindow mainWindow)
                {
                    mainWindow.ToggleWindow();
                }
            }
            else if (messageType == Constants.PowerDisplayApplyProfileMessage())
            {
                // Apply profile by name
                if (messageParts.Length > 1 && _mainWindow is MainWindow mainWindow && mainWindow.ViewModel != null)
                {
                    var profileName = messageParts[1].Trim();
                    Logger.LogInfo($"[NamedPipe] Applying profile: {profileName}");
                    await mainWindow.ViewModel.ApplyProfileByNameAsync(profileName);
                }
            }
            else if (messageType == Constants.PowerDisplayTerminateAppMessage())
            {
                // Terminate the application
                Logger.LogInfo("[NamedPipe] Received terminate message");
                Shutdown();
            }
            else
            {
                Logger.LogWarning($"[NamedPipe] Unknown message type: {messageType}");
            }
        }
    }
}
