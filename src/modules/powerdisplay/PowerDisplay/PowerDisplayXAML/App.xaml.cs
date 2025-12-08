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
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.AppLifecycle;
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
        /// <summary>
        /// Event name for signaling that PowerDisplay process is ready.
        /// Must match the constant in C++ PowerDisplayModuleInterface.
        /// </summary>
        private const string ProcessReadyEventName = "Local\\PowerToys_PowerDisplay_Ready";

        private readonly ISettingsUtils _settingsUtils = SettingsUtils.Default;
        private Window? _mainWindow;
        private int _powerToysRunnerPid;
        private TrayIconService? _trayIconService;

        public App(int runnerPid)
        {
            _powerToysRunnerPid = runnerPid;

            this.InitializeComponent();

            // Ensure types used in XAML are preserved for AOT compilation
            TypePreservation.PreserveTypes();

            // Initialize Logger
            Logger.InitializeLogger("\\PowerDisplay\\Logs");

            // Initialize PowerToys telemetry
            try
            {
                PowerToysTelemetry.Log.WriteEvent(new Telemetry.Events.PowerDisplayStartEvent());
            }
            catch
            {
                // Telemetry errors should not crash the app
            }

            // Initialize language settings
            string appLanguage = LanguageHelper.LoadLanguage();
            if (!string.IsNullOrEmpty(appLanguage))
            {
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = appLanguage;
            }

            // Handle unhandled exceptions
            this.UnhandledException += OnUnhandledException;
        }

        /// <summary>
        /// Handle unhandled exceptions
        /// </summary>
        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            // Try to display error information
            ShowStartupError(e.Exception);

            // Mark exception as handled to prevent app crash
            e.Handled = true;
        }

        /// <summary>
        /// Called when the application is launched
        /// </summary>
        /// <param name="args">Launch arguments</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                // Single instance is already ensured by AppInstance.FindOrRegisterForKey() in Program.cs
                // PID is already parsed in Program.cs and passed to constructor

                // Set up Windows Events monitoring (Awake pattern)
                // Note: PowerDisplay.exe should NOT listen to RefreshMonitorsEvent
                // That event is sent BY PowerDisplay TO Settings UI for one-way notification
                RegisterWindowEvent(Constants.ShowPowerDisplayEvent(), mw => mw.ShowWindow(), "Show");
                RegisterWindowEvent(Constants.TogglePowerDisplayEvent(), mw => mw.ToggleWindow(), "Toggle");
                RegisterEvent(Constants.TerminatePowerDisplayEvent(), () => Environment.Exit(0), "Terminate");
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

                // Monitor Runner process (backup exit mechanism)
                if (_powerToysRunnerPid > 0)
                {
                    Logger.LogInfo($"PowerDisplay started from PowerToys Runner. Runner pid={_powerToysRunnerPid}");

                    RunnerHelper.WaitForPowerToysRunner(_powerToysRunnerPid, () =>
                    {
                        Logger.LogInfo("PowerToys Runner exited. Exiting PowerDisplay");
                        Environment.Exit(0);
                    });
                }
                else
                {
                    Logger.LogInfo("PowerDisplay started in standalone mode");
                }

                // Create main window
                _mainWindow = new MainWindow();

                // Initialize tray icon service
                _trayIconService = new TrayIconService(
                    _settingsUtils,
                    ShowMainWindow,
                    ToggleMainWindow,
                    () => Environment.Exit(0),
                    OpenSettings);
                _trayIconService.SetupTrayIcon();

                // Window visibility depends on launch mode
                bool isStandaloneMode = _powerToysRunnerPid <= 0;

                if (isStandaloneMode)
                {
                    // Standalone mode - activate and show window immediately
                    _mainWindow.Activate();
                    Logger.LogInfo("Window activated (standalone mode)");

                    // Signal ready immediately in standalone mode
                    SignalProcessReady();
                }
                else
                {
                    // PowerToys mode - window remains hidden until show event received
                    Logger.LogInfo("Window created, waiting for show event (PowerToys mode)");

                    // Start background initialization to scan monitors even when hidden
                    // Signal process ready AFTER initialization completes to prevent race condition
                    _ = Task.Run(async () =>
                    {
                        // Give window a moment to finish construction
                        await Task.Delay(100);

                        // Trigger initialization on UI thread and wait for completion
                        var initComplete = new TaskCompletionSource<bool>();
                        _mainWindow?.DispatcherQueue.TryEnqueue(async () =>
                        {
                            try
                            {
                                if (_mainWindow is MainWindow mainWindow)
                                {
                                    await mainWindow.EnsureInitializedAsync();
                                    Logger.LogInfo("Background initialization completed");
                                }

                                initComplete.SetResult(true);
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Background initialization failed: {ex.Message}");
                                initComplete.SetResult(false);
                            }
                        });

                        // Wait for initialization to complete before signaling ready
                        await initComplete.Task;

                        // NOW signal that process is ready to receive events
                        // This ensures window is fully initialized before C++ module can send Toggle/Show events
                        SignalProcessReady();
                        Logger.LogInfo("Process ready signal sent after initialization");
                    });
                }
            }
            catch (Exception ex)
            {
                ShowStartupError(ex);
            }
        }

        /// <summary>
        /// Register a simple event handler (no window access needed)
        /// </summary>
        private void RegisterEvent(string eventName, Action action, string logName)
        {
            NativeEventWaiter.WaitForEventLoop(
                eventName,
                () =>
                {
                    Logger.LogInfo($"[EVENT] {logName} event received");
                    action();
                },
                CancellationToken.None);
        }

        /// <summary>
        /// Register an event handler that operates on MainWindow directly
        /// NativeEventWaiter already marshals to UI thread
        /// </summary>
        private void RegisterWindowEvent(string eventName, Action<MainWindow> action, string logName)
        {
            NativeEventWaiter.WaitForEventLoop(
                eventName,
                () =>
                {
                    Logger.LogInfo($"[EVENT] {logName} event received");
                    if (_mainWindow is MainWindow mainWindow)
                    {
                        action(mainWindow);
                    }
                    else
                    {
                        Logger.LogError($"[EVENT] _mainWindow type mismatch for {logName}");
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
        /// Show startup error
        /// </summary>
        private void ShowStartupError(Exception ex)
        {
            try
            {
                Logger.LogError($"PowerDisplay startup failed: {ex.Message}");

                var errorWindow = new Window { Title = "PowerDisplay - Startup Error" };
                var panel = new StackPanel { Margin = new Thickness(20), Spacing = 16 };

                panel.Children.Add(new TextBlock
                {
                    Text = "PowerDisplay Startup Failed",
                    FontSize = 20,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                });

                panel.Children.Add(new TextBlock
                {
                    Text = $"Error: {ex.Message}",
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap,
                });

                panel.Children.Add(new TextBlock
                {
                    Text = $"Details:\n{ex}",
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                    Margin = new Thickness(0, 10, 0, 0),
                });

                var closeButton = new Button
                {
                    Content = "Close",
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 10, 0, 0),
                };
                closeButton.Click += (_, _) => errorWindow.Close();
                panel.Children.Add(closeButton);

                errorWindow.Content = new ScrollViewer
                {
                    Content = panel,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    MaxHeight = 600,
                    MaxWidth = 800,
                };

                errorWindow.Activate();
            }
            catch
            {
                Environment.Exit(1);
            }
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
            if (_mainWindow is MainWindow mainWindow)
            {
                mainWindow.ShowWindow();
            }
        }

        /// <summary>
        /// Toggle the main window visibility
        /// </summary>
        private void ToggleMainWindow()
        {
            if (_mainWindow is MainWindow mainWindow)
            {
                mainWindow.ToggleWindow();
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

        /// <summary>
        /// Signal that PowerDisplay process is ready to receive events.
        /// Uses a ManualReset event so the C++ module can wait on it.
        /// </summary>
        private static void SignalProcessReady()
        {
            try
            {
                using var readyEvent = new EventWaitHandle(
                    false,
                    EventResetMode.ManualReset,
                    ProcessReadyEventName);
                readyEvent.Set();
                Logger.LogInfo("Signaled process ready event");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to signal process ready event: {ex.Message}");
            }
        }
    }
}
