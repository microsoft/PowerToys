// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;

namespace PowerDisplay
{
    public static partial class Program
    {
        private static App? _app;

        // LibraryImport for AOT compatibility - COM wait constants
        private const uint CowaitDefault = 0;
        private const uint InfiniteTimeout = 0xFFFFFFFF;

        [LibraryImport("ole32.dll")]
        private static partial int CoWaitForMultipleObjects(
            uint dwFlags,
            uint dwTimeout,
            int cHandles,
            nint[] pHandles,
            out uint lpdwIndex);

        [STAThread]
        public static int Main(string[] args)
        {
            // Initialize COM wrappers first (needed for AppInstance)
            WinRT.ComWrappersSupport.InitializeComWrappers();

            // Exit before instance registration so a blocked launch cannot redirect
            // activation to an already-running instance.
            if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredPowerDisplayEnabledValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                return 0;
            }

            // Parse command line arguments:
            // Mode 1 (PowerToys runner): args[0] = runner_pid, args[1] = pipe_name
            // Mode 2 (CLI show-at):      --show-at <x_pixels> <y_pixels>
            int runnerPid = -1;
            string? pipeName = null;
            int? showAtX = null;
            int? showAtY = null;

            if (args.Length >= 2 && args[0] == "--show-at")
            {
                if (int.TryParse(args[1], out int x) && args.Length >= 3 && int.TryParse(args[2], out int y))
                {
                    showAtX = x;
                    showAtY = y;
                }
            }
            else
            {
                if (args.Length >= 1 && int.TryParse(args[0], out int parsedPid))
                {
                    runnerPid = parsedPid;
                }

                if (args.Length >= 2)
                {
                    pipeName = args[1];
                }
            }

            // Single instance check BEFORE logger initialization to avoid creating extra log files
            // Command Palette pattern: check for existing instance first
            var activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            var keyInstance = AppInstance.FindOrRegisterForKey("PowerToys_PowerDisplay_Instance");

            if (!keyInstance.IsCurrent)
            {
                // Another instance exists - write show-at position to temp file if provided,
                // then redirect activation
                if (showAtX.HasValue && showAtY.HasValue)
                {
                    var tempPath = Path.Combine(Path.GetTempPath(), "PowerDisplay_ShowAt.txt");
                    File.WriteAllText(tempPath, $"{showAtX.Value} {showAtY.Value}");
                }

                RedirectActivationTo(activationArgs, keyInstance);
                return 0;
            }

            // This is the primary instance - now initialize logger
            Logger.InitializeLogger("\\PowerDisplay\\Logs");
            Logger.LogInfo("PowerDisplay starting");

            // Register activation handler for future redirects
            keyInstance.Activated += OnActivated;

            Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _app = new App(runnerPid, pipeName, showAtX, showAtY);
            });
            return 0;
        }

        /// <summary>
        /// Redirect activation to existing instance (Command Palette pattern)
        /// Called BEFORE logger is initialized, so no logging here
        /// </summary>
        private static void RedirectActivationTo(AppActivationArguments args, AppInstance keyInstance)
        {
            // Do the redirection on another thread, and use a non-blocking
            // wait method to wait for the redirection to complete.
            using var redirectSemaphore = new Semaphore(0, 1);
            var redirectTimeout = TimeSpan.FromSeconds(10);

            _ = Task.Run(() =>
            {
                using var cts = new CancellationTokenSource(redirectTimeout);
                try
                {
                    keyInstance.RedirectActivationToAsync(args)
                        .AsTask(cts.Token)
                        .GetAwaiter()
                        .GetResult();
                }
                catch
                {
                    // Silently ignore errors - logger not initialized yet
                }
                finally
                {
                    redirectSemaphore.Release();
                }
            });

            // Use CoWaitForMultipleObjects to pump COM messages while waiting
            nint[] handles = [redirectSemaphore.SafeWaitHandle.DangerousGetHandle()];
            _ = CoWaitForMultipleObjects(
                CowaitDefault,
                InfiniteTimeout,
                1,
                handles,
                out _);
        }

        /// <summary>
        /// Called when an existing instance is activated by another process.
        /// This happens when Quick Access or other launchers start the process while one is already running.
        /// We toggle the window to show it - this allows Quick Access launch to work properly.
        /// </summary>
        private static void OnActivated(object? sender, AppActivationArguments args)
        {
            Logger.LogInfo("OnActivated: Redirect activation received");

            if (_app?.MainWindow is MainWindow mainWindow)
            {
                mainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    // Check if a show-at position was written by the redirecting process
                    var tempPath = Path.Combine(Path.GetTempPath(), "PowerDisplay_ShowAt.txt");
                    if (File.Exists(tempPath))
                    {
                        try
                        {
                            var content = File.ReadAllText(tempPath).Trim();
                            File.Delete(tempPath);
                            var parts = content.Split(' ');
                            if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                            {
                                Logger.LogInfo($"OnActivated: Showing window at ({x}, {y}) from temp file");
                                mainWindow.ShowWindowAt(x, y);
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"OnActivated: Failed to read show-at position: {ex.Message}");
                        }
                    }

                    // Fallback: toggle window (original behavior)
                    Logger.LogTrace("OnActivated: Toggling window (no position specified)");
                    mainWindow.ToggleWindow();
                });
            }
            else
            {
                Logger.LogWarning("OnActivated: MainWindow not available");
            }
        }
    }
}
