// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
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
        // Windows Event names (from shared_constants.h)
        private const string ShowPowerDisplayEvent = "Local\\PowerToysPowerDisplay-ShowEvent-d8a4e0e3-2c5b-4a1c-9e7f-8b3d6c1a2f4e";
        private const string TogglePowerDisplayEvent = "Local\\PowerToysPowerDisplay-ToggleEvent-5f1a9c3e-7d2b-4e8f-9a6c-3b5d7e9f1a2c";
        private const string TerminatePowerDisplayEvent = "Local\\PowerToysPowerDisplay-TerminateEvent-7b9c2e1f-8a5d-4c3e-9f6b-2a1d8c5e3b7a";
        private const string RefreshMonitorsEvent = "Local\\PowerToysPowerDisplay-RefreshMonitorsEvent-a3f5c8e7-9d1b-4e2f-8c6a-3b5d7e9f1a2c";
        private const string SettingsUpdatedEvent = "Local\\PowerToysPowerDisplay-SettingsUpdatedEvent-2e4d6f8a-1c3b-5e7f-9a1d-4c6e8f0b2d3e";

        private Window? _mainWindow;
        private int _powerToysRunnerPid;

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
                NativeEventWaiter.WaitForEventLoop(
                    ShowPowerDisplayEvent,
                    () =>
                    {
                        Logger.LogInfo("[EVENT] Show event received");
                        Logger.LogInfo($"[EVENT] _mainWindow is null: {_mainWindow == null}");
                        Logger.LogInfo($"[EVENT] _mainWindow type: {_mainWindow?.GetType().Name}");
                        Logger.LogInfo($"[EVENT] Current thread ID: {Environment.CurrentManagedThreadId}");

                        // Direct call - NativeEventWaiter already marshalled to UI thread
                        // No need for double DispatcherQueue.TryEnqueue
                        if (_mainWindow is MainWindow mainWindow)
                        {
                            Logger.LogInfo("[EVENT] Calling ShowWindow directly");
                            mainWindow.ShowWindow();
                            Logger.LogInfo("[EVENT] ShowWindow returned");
                        }
                        else
                        {
                            Logger.LogError($"[EVENT] _mainWindow type mismatch, actual type: {_mainWindow?.GetType().Name}");
                        }
                    });

                NativeEventWaiter.WaitForEventLoop(
                    TogglePowerDisplayEvent,
                    () =>
                    {
                        Logger.LogInfo("[EVENT] Toggle event received");
                        if (_mainWindow is MainWindow mainWindow)
                        {
                            Logger.LogInfo("[EVENT] Calling ToggleWindow");
                            mainWindow.ToggleWindow();
                        }
                        else
                        {
                            Logger.LogError($"[EVENT] _mainWindow type mismatch for toggle");
                        }
                    });

                NativeEventWaiter.WaitForEventLoop(
                    TerminatePowerDisplayEvent,
                    () =>
                    {
                        Logger.LogInfo("Received terminate event - exiting immediately");
                        Environment.Exit(0);
                    });

                NativeEventWaiter.WaitForEventLoop(
                    RefreshMonitorsEvent,
                    () =>
                    {
                        Logger.LogInfo("Received refresh monitors event");
                        _mainWindow?.DispatcherQueue.TryEnqueue(() =>
                        {
                            if (_mainWindow is MainWindow mainWindow && mainWindow.ViewModel != null)
                            {
                                mainWindow.ViewModel.RefreshCommand?.Execute(null);
                            }
                        });
                    });

                NativeEventWaiter.WaitForEventLoop(
                    SettingsUpdatedEvent,
                    () =>
                    {
                        Logger.LogInfo("Received settings updated event");
                        _mainWindow?.DispatcherQueue.TryEnqueue(() =>
                        {
                            if (_mainWindow is MainWindow mainWindow && mainWindow.ViewModel != null)
                            {
                                mainWindow.ViewModel.ApplySettingsFromUI();
                            }
                        });
                    });

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

                // Window visibility depends on launch mode
                bool isStandaloneMode = _powerToysRunnerPid <= 0;

                if (isStandaloneMode)
                {
                    // Standalone mode - activate and show window immediately
                    _mainWindow.Activate();
                    Logger.LogInfo("Window activated (standalone mode)");
                }
                else
                {
                    // PowerToys mode - window remains hidden until show event received
                    Logger.LogInfo("Window created, waiting for show event (PowerToys mode)");

                    // Start background initialization to scan monitors even when hidden
                    _ = Task.Run(async () =>
                    {
                        // Give window a moment to finish construction
                        await Task.Delay(500);

                        // Trigger initialization on UI thread
                        _mainWindow?.DispatcherQueue.TryEnqueue(async () =>
                        {
                            if (_mainWindow is MainWindow mainWindow)
                            {
                                await mainWindow.EnsureInitializedAsync();
                                Logger.LogInfo("Background initialization completed");
                            }
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                ShowStartupError(ex);
            }
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
            Environment.Exit(0);
        }
    }
}
