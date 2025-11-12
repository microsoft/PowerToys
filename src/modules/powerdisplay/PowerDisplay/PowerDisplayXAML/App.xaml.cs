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
    public partial class App : Application
    {
        private Window? _mainWindow;
        private int _powerToysRunnerPid;
        private string _pipeUuid = string.Empty;
        private static Mutex? _mutex;

        // Bidirectional named pipes for IPC
        private static System.IO.Pipes.NamedPipeClientStream? _readPipe;  // Read from ModuleInterface (OUT pipe)
        private static System.IO.Pipes.NamedPipeClientStream? _writePipe; // Write to ModuleInterface (IN pipe)
        private static Thread? _messageReceiverThread;
        private static bool _stopReceiver;

        /// <summary>
        /// Sends IPC message to Settings UI via ModuleInterface
        /// </summary>
        public static void SendIPCMessage(string message)
        {
            try
            {
                if (_writePipe != null && _writePipe.IsConnected)
                {
                    var writer = new System.IO.StreamWriter(_writePipe) { AutoFlush = true };
                    writer.WriteLine(message);
                    Logger.LogTrace($"Sent IPC message: {message}");
                }
                else
                {
                    Logger.LogWarning("Cannot send IPC message: pipe not connected");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to send IPC message: {ex.Message}");
            }
        }

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
                // Use Mutex to ensure only one PowerDisplay instance is running
                _mutex = new Mutex(true, "PowerDisplay", out bool isNewInstance);

                if (!isNewInstance)
                {
                    // PowerDisplay is already running, exit current instance
                    Logger.LogInfo("PowerDisplay is already running. Exiting duplicate instance.");
                    Environment.Exit(0);
                    return;
                }

                // Ensure Mutex is released when app exits
                AppDomain.CurrentDomain.ProcessExit += (_, _) => _mutex?.ReleaseMutex();

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
                    // Async pipe connection in background - don't block UI thread
                    _ = Task.Run(() => InitializeBidirectionalPipes(_pipeUuid));
                    Logger.LogInfo("Starting IPC pipe connection in background");
                }
                else
                {
                    Logger.LogInfo("Running in standalone mode, IPC disabled");
                }

                // Create main window
                _mainWindow = new MainWindow();

                // FIX BUG #5: Window visibility depends on launch mode
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
                }
            }
            catch (Exception ex)
            {
                ShowStartupError(ex);
            }
        }

        /// <summary>
        /// Initialize bidirectional named pipes for IPC with ModuleInterface
        /// </summary>
        private void InitializeBidirectionalPipes(string pipeUuid)
        {
            try
            {
                // Pipe names based on UUID from ModuleInterface
                string pipeNameIn = $"powertoys_powerdisplay_{pipeUuid}_in";   // Write to this (ModuleInterface reads)
                string pipeNameOut = $"powertoys_powerdisplay_{pipeUuid}_out"; // Read from this (ModuleInterface writes)

                Logger.LogInfo($"Connecting to pipes: IN={pipeNameIn}, OUT={pipeNameOut}");

                // Connect to write pipe (IN pipe from ModuleInterface perspective)
                _writePipe = new System.IO.Pipes.NamedPipeClientStream(
                    ".",
                    pipeNameIn,
                    System.IO.Pipes.PipeDirection.Out);
                _writePipe.Connect(2000); // 2 second timeout (reduced from 5s, we're in background thread)

                // Connect to read pipe (OUT pipe from ModuleInterface perspective)
                _readPipe = new System.IO.Pipes.NamedPipeClientStream(
                    ".",
                    pipeNameOut,
                    System.IO.Pipes.PipeDirection.In);
                _readPipe.Connect(2000); // 2 second timeout (reduced from 5s)

                Logger.LogInfo("Successfully connected to bidirectional pipes");

                // Start message receiver thread
                _stopReceiver = false;
                _messageReceiverThread = new Thread(MessageReceiverThreadProc)
                {
                    IsBackground = true,
                    Name = "PowerDisplay IPC Receiver",
                };
                _messageReceiverThread.Start();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to initialize bidirectional pipes: {ex.Message}. App will continue in standalone mode.");

                // Clean up on failure
                try
                {
                    _writePipe?.Dispose();
                    _readPipe?.Dispose();
                    _writePipe = null;
                    _readPipe = null;
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        /// <summary>
        /// Message receiver thread procedure
        /// </summary>
        private void MessageReceiverThreadProc()
        {
            Logger.LogInfo("Message receiver thread started");

            try
            {
                if (_readPipe == null || !_readPipe.IsConnected)
                {
                    Logger.LogError("Read pipe is not connected");
                    return;
                }

                var reader = new System.IO.StreamReader(_readPipe);

                while (!_stopReceiver && _readPipe.IsConnected)
                {
                    try
                    {
                        string? message = reader.ReadLine();
                        if (message != null)
                        {
                            OnIPCMessageReceived(message);
                        }
                    }
                    catch (System.IO.IOException)
                    {
                        // Pipe disconnected
                        Logger.LogWarning("Pipe disconnected");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Error reading from pipe: {ex.Message}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Message receiver thread error: {ex.Message}");
            }

            Logger.LogInfo("Message receiver thread exiting");
        }

        /// <summary>
        /// Handle IPC messages received from ModuleInterface/Settings UI
        /// </summary>
        private void OnIPCMessageReceived(string message)
        {
            try
            {
                Logger.LogInfo($"Received IPC message: {message}");

                // Parse JSON message and handle commands (using source-generated context for AOT)
                // Expected format: {"action": "command_name", ...}
                var ipcMessage = System.Text.Json.JsonSerializer.Deserialize(message, AppJsonContext.Default.IPCMessageAction);
                if (ipcMessage?.Action != null)
                {
                    string action = ipcMessage.Action;

                    switch (action)
                    {
                        case "show_window":
                            Logger.LogInfo("Received show_window command");

                            // FIX BUG #3: Implement window show logic
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

                            // FIX BUG #3: Implement window toggle logic
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

                            // FIX BUG #3: Implement monitor refresh logic
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

                            // FIX BUG #3: Implement settings update logic
                            _mainWindow?.DispatcherQueue.TryEnqueue(() =>
                            {
                                if (_mainWindow is MainWindow mainWindow && mainWindow.ViewModel != null)
                                {
                                    // Reload settings from file
                                    _ = mainWindow.ViewModel.ReloadMonitorSettingsAsync();
                                }
                            });
                            break;

                        case "terminate":
                            Logger.LogInfo("Received terminate command");

                            // FIX BUG #3: Implement graceful shutdown
                            _mainWindow?.DispatcherQueue.TryEnqueue(() =>
                            {
                                Shutdown();
                            });
                            break;

                        default:
                            Logger.LogWarning($"Unknown action received: {action}");
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
        /// Quick cleanup when application exits
        /// </summary>
        public void Shutdown()
        {
            try
            {
                // Start timeout mechanism, ensure exit within 1 second
                var timeoutTimer = new System.Threading.Timer(
                    _ =>
                    {
                        Logger.LogWarning("Shutdown timeout reached, forcing exit");
                        Environment.Exit(0);
                    },
                    null,
                    1000,
                    System.Threading.Timeout.Infinite);

                // Immediately notify MainWindow that program is exiting, enable fast shutdown mode
                if (_mainWindow is MainWindow mainWindow)
                {
                    mainWindow.SetExiting();
                    mainWindow.FastShutdown();
                }

                _mainWindow = null;

                // Clean up IPC pipes
                try
                {
                    _stopReceiver = true;
                    _messageReceiverThread?.Join(1000); // Wait max 1 second

                    _readPipe?.Close();
                    _readPipe?.Dispose();
                    _readPipe = null;

                    _writePipe?.Close();
                    _writePipe?.Dispose();
                    _writePipe = null;
                }
                catch
                {
                    // Ignore IPC cleanup errors
                }

                // Immediately release Mutex
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
                _mutex = null;

                // Cancel timeout timer
                timeoutTimer?.Dispose();
            }
            catch
            {
                // Ignore cleanup errors, ensure exit
                Environment.Exit(0);
            }
        }

        /// <summary>
        /// Force exit application, ensure complete termination
        /// </summary>
        private void ForceExit()
        {
            try
            {
                // Immediately start timeout mechanism, must exit within 500ms
                var emergencyTimer = new System.Threading.Timer(
                    _ =>
                    {
                        Logger.LogWarning("Emergency exit timeout reached, terminating process");
                        Environment.Exit(0);
                    },
                    null,
                    500,
                    System.Threading.Timeout.Infinite);

                PerformForceExit();
            }
            catch
            {
                // If all other methods fail, immediately force exit process
                Environment.Exit(0);
            }
        }

        /// <summary>
        /// Perform fast exit operation
        /// </summary>
        private void PerformForceExit()
        {
            try
            {
                // Fast shutdown
                Shutdown();

                // Immediately exit
                Environment.Exit(0);
            }
            catch
            {
                // Ensure exit
                Environment.Exit(0);
            }
        }
    }
}
