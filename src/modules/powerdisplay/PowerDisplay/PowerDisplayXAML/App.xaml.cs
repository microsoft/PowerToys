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

namespace PowerDisplay
{
    /// <summary>
    /// PowerDisplay application main class
    /// </summary>
#pragma warning disable CA1001 // CancellationTokenSource is disposed in Shutdown/ForceExit methods
    public partial class App : Application
#pragma warning restore CA1001
    {
        private Window? _mainWindow;
        private int _powerToysRunnerPid;
        private string _pipeUuid = string.Empty;
        private CancellationTokenSource? _ipcCancellationTokenSource;

        public App(int runnerPid, string pipeUuid)
        {
            _powerToysRunnerPid = runnerPid;
            _pipeUuid = pipeUuid;

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
                // No need for additional Mutex check here

                // Parse command line arguments
                var cmdArgs = Environment.GetCommandLineArgs();
                if (cmdArgs?.Length > 1)
                {
                    // Support two formats: direct PID or --pid PID
                    int pidValue = -1;

                    // Check if using --pid format
                    for (int i = 1; i < cmdArgs.Length - 1; i++)
                    {
                        if (cmdArgs[i] == "--pid" && int.TryParse(cmdArgs[i + 1], out pidValue))
                        {
                            break;
                        }
                    }

                    // If not --pid format, try parsing last argument (compatible with old format)
                    if (pidValue == -1 && cmdArgs.Length > 1)
                    {
                        _ = int.TryParse(cmdArgs[cmdArgs.Length - 1], out pidValue);
                    }

                    if (pidValue > 0)
                    {
                        _powerToysRunnerPid = pidValue;

                        // Started from PowerToys Runner
                        Logger.LogInfo($"PowerDisplay started from PowerToys Runner. Runner pid={_powerToysRunnerPid}");

                        // Monitor parent process
                        RunnerHelper.WaitForPowerToysRunner(_powerToysRunnerPid, () =>
                        {
                            Logger.LogInfo("PowerToys Runner exited. Exiting PowerDisplay");
                            ForceExit();
                        });
                    }
                }
                else
                {
                    // Standalone mode
                    Logger.LogInfo("PowerDisplay started detached from PowerToys Runner.");
                    _powerToysRunnerPid = -1;
                }

                // Initialize IPC in background (non-blocking)
                // Only connect pipes when launched from PowerToys (not standalone)
                bool isIPCMode = !string.IsNullOrEmpty(_pipeUuid) && _powerToysRunnerPid != -1;

                if (isIPCMode)
                {
                    // Start IPC message listener in background
                    _ipcCancellationTokenSource = new CancellationTokenSource();
                    _ = Task.Run(() => StartIPCListener(_pipeUuid, _ipcCancellationTokenSource.Token));
                    Logger.LogInfo("Starting IPC pipe listener in background");
                }
                else
                {
                    Logger.LogInfo("Running in standalone mode, IPC disabled");
                }

                // Create main window
                _mainWindow = new MainWindow();

                // Window visibility depends on launch mode
                // - IPC mode (launched by PowerToys Runner): Start hidden, wait for show_window IPC command
                // - Standalone mode (no command-line args): Show window immediately
                if (!isIPCMode)
                {
                    // Standalone mode - activate and show window
                    _mainWindow.Activate();
                    Logger.LogInfo("Window activated (standalone mode)");
                }
                else
                {
                    // IPC mode - window remains inactive (hidden) until show_window command received
                    Logger.LogInfo("Window created but not activated (IPC mode - waiting for show_window command)");

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
                                Logger.LogInfo("Background initialization completed (IPC mode)");
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
        /// Start IPC listener to receive commands from ModuleInterface
        /// </summary>
        private async Task StartIPCListener(string pipeUuid, CancellationToken cancellationToken)
        {
            try
            {
                string pipeName = $"powertoys_powerdisplay_{pipeUuid}";
                Logger.LogInfo($"Connecting to pipe: {pipeName}");

                await NamedPipeProcessor.ProcessNamedPipeAsync(
                    pipeName,
                    TimeSpan.FromSeconds(5),
                    OnIPCMessageReceived,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Logger.LogInfo("IPC listener cancelled");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in IPC listener: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle IPC messages received from ModuleInterface
        /// </summary>
        private void OnIPCMessageReceived(string message)
        {
            try
            {
                Logger.LogInfo($"Received IPC message: {message}");

                // Parse JSON command
                var json = System.Text.Json.JsonDocument.Parse(message);
                var root = json.RootElement;

                if (root.TryGetProperty("action", out var actionElement))
                {
                    string action = actionElement.GetString() ?? string.Empty;

                    switch (action)
                    {
                        case "show_window":
                            Logger.LogInfo("Received show_window command");
                            _mainWindow?.DispatcherQueue.TryEnqueue(() =>
                            {
                                if (_mainWindow is MainWindow mainWindow)
                                {
                                    mainWindow.ShowWindow();
                                }
                            });
                            break;

                        case "toggle_window":
                            Logger.LogInfo("Received toggle_window command");
                            _mainWindow?.DispatcherQueue.TryEnqueue(() =>
                            {
                                if (_mainWindow is MainWindow mainWindow)
                                {
                                    if (mainWindow.IsWindowVisible())
                                    {
                                        mainWindow.HideWindow();
                                    }
                                    else
                                    {
                                        mainWindow.ShowWindow();
                                    }
                                }
                            });
                            break;

                        case "refresh_monitors":
                            Logger.LogInfo("Received refresh_monitors command");
                            _mainWindow?.DispatcherQueue.TryEnqueue(() =>
                            {
                                if (_mainWindow is MainWindow mainWindow && mainWindow.ViewModel != null)
                                {
                                    mainWindow.ViewModel.RefreshCommand?.Execute(null);
                                }
                            });
                            break;

                        case "settings_updated":
                            Logger.LogInfo("Received settings_updated command");
                            _mainWindow?.DispatcherQueue.TryEnqueue(() =>
                            {
                                if (_mainWindow is MainWindow mainWindow && mainWindow.ViewModel != null)
                                {
                                    _ = mainWindow.ViewModel.ReloadMonitorSettingsAsync();
                                }
                            });
                            break;

                        case "terminate":
                            Logger.LogInfo("Received terminate command");
                            _mainWindow?.DispatcherQueue.TryEnqueue(() =>
                            {
                                Shutdown();
                            });
                            break;

                        default:
                            Logger.LogWarning($"Unknown action: {action}");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error processing IPC message: {ex.Message}");
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
        /// Shutdown application (simplified version following other PowerToys modules pattern)
        /// </summary>
        public void Shutdown()
        {
            Logger.LogInfo("PowerDisplay shutting down");

            // Cancel IPC listener
            _ipcCancellationTokenSource?.Cancel();
            _ipcCancellationTokenSource?.Dispose();

            // Exit immediately - OS will clean up all resources (pipes, threads, windows, etc.)
            // Single instance is managed by AppInstance in Program.cs, no manual cleanup needed
            Environment.Exit(0);
        }

        /// <summary>
        /// Force exit when PowerToys Runner exits
        /// </summary>
        private void ForceExit()
        {
            Logger.LogInfo("PowerToys Runner exited, forcing shutdown");

            // Cancel IPC listener
            _ipcCancellationTokenSource?.Cancel();
            _ipcCancellationTokenSource?.Dispose();

            Environment.Exit(0);
        }
    }
}
