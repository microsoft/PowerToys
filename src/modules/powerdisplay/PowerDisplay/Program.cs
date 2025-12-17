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
            Logger.InitializeLogger("\\PowerDisplay\\Logs");
            Logger.LogInfo("=== PowerDisplay Process Starting ===");
            Logger.LogInfo($"Main: Process ID = {Environment.ProcessId}");
            Logger.LogInfo($"Main: Command line args count = {args.Length}");

            for (int i = 0; i < args.Length; i++)
            {
                Logger.LogInfo($"Main: args[{i}] = '{args[i]}'");
            }

            Logger.LogTrace("Main: Initializing COM wrappers");
            WinRT.ComWrappersSupport.InitializeComWrappers();

            // Parse command line arguments: args[0] = runner_pid (Awake pattern)
            int runnerPid = -1;

            if (args.Length >= 1)
            {
                if (int.TryParse(args[0], out int parsedPid))
                {
                    runnerPid = parsedPid;
                    Logger.LogInfo($"Main: Parsed runner_pid={runnerPid} from args[0]");
                }
                else
                {
                    Logger.LogWarning($"Main: Failed to parse PID from args[0]: '{args[0]}'");
                }
            }
            else
            {
                Logger.LogWarning("Main: No command line args provided. Running in standalone mode.");
            }

            // Single instance check with redirection (Command Palette pattern)
            var isRedirect = DecideRedirection();
            if (isRedirect)
            {
                Logger.LogInfo("Main: Redirected to existing instance, exiting");
                return 0;
            }

            Logger.LogInfo("Main: This is the primary instance, starting application");
            Microsoft.UI.Xaml.Application.Start((p) =>
            {
                Logger.LogTrace("Main: Application.Start callback - setting up SynchronizationContext");
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                Logger.LogTrace("Main: Creating App instance");
                _app = new App(runnerPid);
                Logger.LogTrace("Main: App instance created");
            });

            Logger.LogInfo("Main: Application.Start returned, process ending");
            return 0;
        }

        /// <summary>
        /// Decide whether to redirect activation to an existing instance (Command Palette pattern)
        /// </summary>
        private static bool DecideRedirection()
        {
            Logger.LogTrace("DecideRedirection: Checking for existing instance");
            var isRedirect = false;
            var activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            var keyInstance = AppInstance.FindOrRegisterForKey("PowerToys_PowerDisplay_Instance");

            if (keyInstance.IsCurrent)
            {
                Logger.LogInfo("DecideRedirection: This is the primary instance (IsCurrent=true)");
                keyInstance.Activated += OnActivated;
            }
            else
            {
                Logger.LogInfo("DecideRedirection: Another instance exists, redirecting activation");
                isRedirect = true;
                RedirectActivationTo(activationArgs, keyInstance);
            }

            return isRedirect;
        }

        /// <summary>
        /// Redirect activation to existing instance (Command Palette pattern)
        /// </summary>
        private static void RedirectActivationTo(AppActivationArguments args, AppInstance keyInstance)
        {
            Logger.LogInfo("RedirectActivationTo: Starting redirection to existing instance");

            // Do the redirection on another thread, and use a non-blocking
            // wait method to wait for the redirection to complete.
            using var redirectSemaphore = new Semaphore(0, 1);
            var redirectTimeout = TimeSpan.FromSeconds(10);

            _ = Task.Run(() =>
            {
                using var cts = new CancellationTokenSource(redirectTimeout);
                try
                {
                    Logger.LogTrace("RedirectActivationTo: Calling RedirectActivationToAsync");
                    keyInstance.RedirectActivationToAsync(args)
                        .AsTask(cts.Token)
                        .GetAwaiter()
                        .GetResult();
                    Logger.LogInfo("RedirectActivationTo: Redirection completed successfully");
                }
                catch (OperationCanceledException)
                {
                    Logger.LogError($"RedirectActivationTo: Timed out after {redirectTimeout}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"RedirectActivationTo: Failed - {ex.Message}");
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

            Logger.LogTrace("RedirectActivationTo: Redirection wait completed");
        }

        /// <summary>
        /// Called when an existing instance is activated by another process
        /// </summary>
        private static void OnActivated(object? sender, AppActivationArguments args)
        {
            Logger.LogInfo("OnActivated: Received activation from another instance");

            // Toggle the window visibility when activated by another instance
            if (_app?.MainWindow is MainWindow mainWindow)
            {
                Logger.LogInfo("OnActivated: Showing/toggling main window");
                mainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    Logger.LogTrace("OnActivated: Executing ShowWindow on UI thread");
                    mainWindow.ShowWindow();
                });
            }
            else
            {
                Logger.LogWarning("OnActivated: MainWindow not available");
            }
        }
    }
}
