// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using KeyboardManagerEditorUI.Helpers;
using KeyboardManagerEditorUI.Settings;
using ManagedCommon;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace KeyboardManagerEditorUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private EditorLifetime? _editorLifetime;
        private Process? _parentProcess;
        private DispatcherQueue? _dispatcherQueue;
        private int _parentExitShutdownQueued;

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            // Initialize the logger synchronously, before anything else can log. Doing this on a
            // background task races window creation, so a failure during startup leaves no trace
            // at all - which is exactly what happened in #49399.
            Logger.InitializeLogger("\\Keyboard Manager\\WinUI3Editor\\Logs");

            try
            {
                // The classic and WinUI editors share one instance marker. Only the process that
                // creates it owns the engine-suspension event and is therefore allowed to reset it.
                _editorLifetime = EditorLifetime.TryStart();

                this.InitializeComponent();
                UnhandledException += App_UnhandledException;

                if (_editorLifetime is not null)
                {
                    AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
                    AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                    SettingsManager.CorrelateServiceAndEditorMappings();
                }
            }
            catch
            {
                StopEditorLifetime();
                throw;
            }
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            if (_editorLifetime is null)
            {
                bool activatedExistingEditor = TryActivateExistingEditorWindow();
                Logger.LogInfo(activatedExistingEditor
                    ? "Activated the existing Keyboard Manager editor instance"
                    : "Another Keyboard Manager editor instance is already running; unable to activate its window");
                Exit();
                return;
            }

            Logger.LogInfo("keyboard-manager WinUI3 editor is creating its main window");

            try
            {
                MainWindow = new MainWindow();
                _dispatcherQueue = MainWindow.DispatcherQueue;

                MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    MainWindow.Activate();
                    MainWindow.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
                    {
                        (MainWindow.Content as FrameworkElement)?.UpdateLayout();
                    });
                });

                string parentProcessArgument = args.Arguments;
                if (string.IsNullOrWhiteSpace(parentProcessArgument))
                {
                    string[] commandLineArguments = Environment.GetCommandLineArgs();
                    if (commandLineArguments.Length == 2)
                    {
                        parentProcessArgument = commandLineArguments[1];
                    }
                }

                MonitorParentProcess(parentProcessArgument);

                Logger.LogInfo("keyboard-manager WinUI3 editor window is launched");
            }
            catch
            {
                StopEditorLifetime();
                throw;
            }
        }

        /// <summary>
        /// Log the unhandled exception for the editor.
        /// </summary>
        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            try
            {
                Logger.LogError("Unhandled exception", e.Exception);
            }
            finally
            {
                StopEditorLifetime();
            }
        }

        private void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            StopEditorLifetime();
        }

        private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            try
            {
                if (e.ExceptionObject is Exception exception)
                {
                    Logger.LogError("Unhandled application-domain exception", exception);
                }
                else
                {
                    Logger.LogError($"Unhandled application-domain exception: {e.ExceptionObject}");
                }
            }
            finally
            {
                StopEditorLifetime();
            }
        }

        internal void StopEditorLifetime()
        {
            DetachParentProcess();

            EditorLifetime? editorLifetime = Interlocked.Exchange(ref _editorLifetime, null);
            if (editorLifetime is null)
            {
                return;
            }

            AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_ProcessExit;
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
            editorLifetime.Dispose();
        }

        private void MonitorParentProcess(string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
            {
                return;
            }

            string parentProcessIdArgument = arguments.Trim();
            if (!int.TryParse(parentProcessIdArgument, NumberStyles.None, CultureInfo.InvariantCulture, out int parentProcessId) || parentProcessId <= 0)
            {
                Logger.LogWarning($"Ignoring invalid Keyboard Manager editor parent process argument: {arguments}");
                return;
            }

            Process? parentProcess = null;
            try
            {
                parentProcess = Process.GetProcessById(parentProcessId);
                _parentProcess = parentProcess;
                parentProcess.Exited += ParentProcess_Exited;
                parentProcess.EnableRaisingEvents = true;

                if (parentProcess.HasExited)
                {
                    RequestParentExitShutdown();
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
            {
                DetachParentProcess();
                parentProcess?.Dispose();
                Logger.LogInfo($"Keyboard Manager editor parent process {parentProcessId} is unavailable: {ex.Message}");
                RequestParentExitShutdown();
            }
        }

        private void ParentProcess_Exited(object? sender, EventArgs e)
        {
            RequestParentExitShutdown();
        }

        private void RequestParentExitShutdown()
        {
            if (Interlocked.Exchange(ref _parentExitShutdownQueued, 1) != 0)
            {
                return;
            }

            DispatcherQueue? dispatcherQueue = _dispatcherQueue;
            if (dispatcherQueue is null || !dispatcherQueue.TryEnqueue(ShutdownAfterParentExit))
            {
                Logger.LogWarning("Failed to queue Keyboard Manager editor shutdown after its parent process exited");
            }
        }

        private void ShutdownAfterParentExit()
        {
            try
            {
                Logger.LogInfo("Keyboard Manager editor parent process exited; closing the editor");
                MainWindow?.Close();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to close the Keyboard Manager editor window after its parent process exited", ex);
            }
            finally
            {
                StopEditorLifetime();
                Exit();
            }
        }

        private void DetachParentProcess()
        {
            Process? parentProcess = Interlocked.Exchange(ref _parentProcess, null);
            if (parentProcess is null)
            {
                return;
            }

            try
            {
                parentProcess.Exited -= ParentProcess_Exited;
            }
            finally
            {
                parentProcess.Dispose();
            }
        }

        private static bool TryActivateExistingEditorWindow()
        {
            bool activated = false;
            int currentProcessId = Environment.ProcessId;

            _ = EnumWindows(
                (window, parameter) =>
                {
                    if (!IsWindowVisible(window))
                    {
                        return true;
                    }

                    uint windowThreadId = GetWindowThreadProcessId(window, out uint windowProcessId);
                    if (windowThreadId == 0 || windowProcessId == 0 || windowProcessId == currentProcessId)
                    {
                        return true;
                    }

                    try
                    {
                        using Process process = Process.GetProcessById((int)windowProcessId);
                        if (!string.Equals(process.ProcessName, "PowerToys.KeyboardManagerEditorUI", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(process.ProcessName, "PowerToys.KeyboardManagerEditor", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }

                        _ = ShowWindow(window, ShowWindowRestore);
                        _ = SetForegroundWindow(window);
                        activated = true;
                        return false;
                    }
                    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
                    {
                        return true;
                    }
                },
                IntPtr.Zero);

            return activated;
        }

        internal static MainWindow MainWindow { get; private set; } = null!;

        private const int ShowWindowRestore = 9;

        private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr window);

        private sealed class EditorLifetime : IDisposable
        {
            private const string InstanceMutexName = @"Local\PowerToys_KBMEditor_InstanceMutex";
            private const string EditorWindowEventName = "PowerToys_KeyboardManager_Event_EditorWindow";

            private Mutex? _instanceMarker;
            private EventWaitHandle? _editorWindowEvent;
            private bool _ownsInstanceMarker;

            private EditorLifetime(Mutex instanceMarker, EventWaitHandle editorWindowEvent, bool ownsInstanceMarker)
            {
                _instanceMarker = instanceMarker;
                _editorWindowEvent = editorWindowEvent;
                _ownsInstanceMarker = ownsInstanceMarker;
            }

            public static EditorLifetime? TryStart()
            {
                Mutex? instanceMarker = null;
                EventWaitHandle? editorWindowEvent = null;
                bool ownsInstanceMarker = false;

                try
                {
                    // The editor intentionally owns this mutex. A normal UI-thread shutdown releases
                    // it, while a crash leaves an abandoned mutex that the engine can detect and use
                    // to clear a stale editor-window event.
                    bool createdNew;
                    try
                    {
                        instanceMarker = new Mutex(true, InstanceMutexName, out createdNew);
                        ownsInstanceMarker = createdNew;
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        // An elevated editor can own a marker that this process cannot open. Treat
                        // that as an existing instance and, critically, do not touch its event.
                        Logger.LogWarning($"Unable to open the Keyboard Manager editor instance marker: {ex.Message}");
                        return null;
                    }

                    if (!createdNew)
                    {
                        instanceMarker.Dispose();
                        return null;
                    }

                    editorWindowEvent = new EventWaitHandle(false, EventResetMode.ManualReset, EditorWindowEventName);
                    if (!editorWindowEvent.Set())
                    {
                        throw new InvalidOperationException("Failed to signal the Keyboard Manager editor window event");
                    }

                    Logger.LogInfo("Signaled the Keyboard Manager editor window event to suspend the engine");
                    return new EditorLifetime(instanceMarker, editorWindowEvent, ownsInstanceMarker);
                }
                catch
                {
                    if (editorWindowEvent is not null)
                    {
                        try
                        {
                            editorWindowEvent.Reset();
                        }
                        finally
                        {
                            editorWindowEvent.Dispose();
                        }
                    }

                    if (ownsInstanceMarker && instanceMarker is not null)
                    {
                        try
                        {
                            instanceMarker.ReleaseMutex();
                        }
                        catch (ApplicationException ex)
                        {
                            Logger.LogWarning($"Failed to release the Keyboard Manager editor instance mutex: {ex.Message}");
                        }
                    }

                    instanceMarker?.Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                EventWaitHandle? editorWindowEvent = Interlocked.Exchange(ref _editorWindowEvent, null);
                Mutex? instanceMarker = Interlocked.Exchange(ref _instanceMarker, null);
                bool ownsInstanceMarker = _ownsInstanceMarker;
                _ownsInstanceMarker = false;

                try
                {
                    if (editorWindowEvent is not null)
                    {
                        editorWindowEvent.Reset();
                        Logger.LogInfo("Reset the Keyboard Manager editor window event to resume the engine");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to reset the Keyboard Manager editor window event", ex);
                }
                finally
                {
                    editorWindowEvent?.Dispose();

                    if (ownsInstanceMarker && instanceMarker is not null)
                    {
                        try
                        {
                            instanceMarker.ReleaseMutex();
                        }
                        catch (ApplicationException ex)
                        {
                            Logger.LogWarning($"Failed to release the Keyboard Manager editor instance mutex: {ex.Message}");
                        }
                    }

                    instanceMarker?.Dispose();
                    GC.SuppressFinalize(this);
                }
            }
        }
    }
}
