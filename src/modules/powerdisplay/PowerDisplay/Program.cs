// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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

            // Single instance check BEFORE logger initialization to avoid creating extra log files
            // Command Palette pattern: check for existing instance first
            var activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            var keyInstance = AppInstance.FindOrRegisterForKey("PowerToys_PowerDisplay_Instance");

            if (!keyInstance.IsCurrent)
            {
                // Another instance exists - redirect and exit WITHOUT initializing logger
                // This prevents creation of extra log files for short-lived redirect processes
                RedirectActivationTo(activationArgs, keyInstance);
                return 0;
            }

            // This is the primary instance - now initialize logger
            Logger.InitializeLogger("\\PowerDisplay\\Logs");
            Logger.LogInfo("PowerDisplay starting");

            // Register activation handler for future redirects
            keyInstance.Activated += OnActivated;

            // Parse command line arguments: args[0] = runner_pid (Awake pattern)
            int runnerPid = -1;

            if (args.Length >= 1)
            {
                if (int.TryParse(args[0], out int parsedPid))
                {
                    runnerPid = parsedPid;
                }
            }

            Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _app = new App(runnerPid);
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
        /// This happens when EnsureProcessRunning() launches a new process while one is already running.
        /// We intentionally don't show the window here - window visibility should only be controlled via:
        /// - Toggle event (hotkey, tray icon click, Settings UI Launch button)
        /// - Standalone mode startup (handled in OnLaunched)
        /// </summary>
        private static void OnActivated(object? sender, AppActivationArguments args)
        {
            Logger.LogInfo("OnActivated: Redirect activation received - window visibility unchanged");
        }
    }
}
