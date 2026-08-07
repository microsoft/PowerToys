// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            // The classic and WinUI editors share one instance marker. Only the process that
            // creates it owns the engine-suspension event and is therefore allowed to reset it.
            _editorLifetime = EditorLifetime.TryStart();

            this.InitializeComponent();
            UnhandledException += App_UnhandledException;

            if (_editorLifetime is not null)
            {
                SettingsManager.CorrelateServiceAndEditorMappings();
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
                Logger.LogInfo("Another Keyboard Manager editor instance is already running");
                Exit();
                return;
            }

            Logger.LogInfo("keyboard-manager WinUI3 editor is creating its main window");

            MainWindow = new MainWindow();

            MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                MainWindow.Activate();
                MainWindow.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
                {
                    (MainWindow.Content as FrameworkElement)?.UpdateLayout();
                });
            });

            string[] commandLineArguments = Environment.GetCommandLineArgs();
            if (commandLineArguments.Length > 1 &&
                int.TryParse(commandLineArguments[1], out int parentProcessId) &&
                parentProcessId > 0)
            {
                RunnerHelper.WaitForPowerToysRunner(
                    parentProcessId,
                    () => MainWindow.DispatcherQueue.TryEnqueue(() => MainWindow.Close()));
            }

            Logger.LogInfo("keyboard-manager WinUI3 editor window is launched");
        }

        /// <summary>
        /// Log the unhandled exception for the editor.
        /// </summary>
        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logger.LogError("Unhandled exception", e.Exception);
        }

        internal void StopEditorLifetime()
        {
            EditorLifetime? editorLifetime = Interlocked.Exchange(ref _editorLifetime, null);
            editorLifetime?.Dispose();
        }

        internal static MainWindow MainWindow { get; private set; } = null!;

        private sealed class EditorLifetime : IDisposable
        {
            private const string InstanceMutexName = @"Local\PowerToys_KBMEditor_InstanceMutex";
            private const string EditorWindowEventName = "PowerToys_KeyboardManager_Event_EditorWindow";

            private readonly Mutex _instanceMarker;
            private readonly EventWaitHandle _editorWindowEvent;

            private EditorLifetime(Mutex instanceMarker, EventWaitHandle editorWindowEvent)
            {
                _instanceMarker = instanceMarker;
                _editorWindowEvent = editorWindowEvent;
            }

            public static EditorLifetime? TryStart()
            {
                Mutex instanceMarker;
                bool createdNew;
                try
                {
                    // The editor intentionally owns this mutex. A normal UI-thread shutdown releases
                    // it, while a crash leaves an abandoned mutex that the engine can detect and use
                    // to clear a stale editor-window event.
                    instanceMarker = new Mutex(true, InstanceMutexName, out createdNew);
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

                EventWaitHandle? editorWindowEvent = null;
                try
                {
                    editorWindowEvent = new EventWaitHandle(false, EventResetMode.ManualReset, EditorWindowEventName);
                    if (!editorWindowEvent.Set())
                    {
                        throw new InvalidOperationException("Failed to signal the Keyboard Manager editor window event");
                    }

                    Logger.LogInfo("Signaled the Keyboard Manager editor window event to suspend the engine");
                    return new EditorLifetime(instanceMarker, editorWindowEvent);
                }
                catch
                {
                    try
                    {
                        editorWindowEvent?.Reset();
                    }
                    finally
                    {
                        editorWindowEvent?.Dispose();
                        instanceMarker.ReleaseMutex();
                        instanceMarker.Dispose();
                    }

                    throw;
                }
            }

            public void Dispose()
            {
                try
                {
                    _editorWindowEvent.Reset();
                    Logger.LogInfo("Reset the Keyboard Manager editor window event to resume the engine");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to reset the Keyboard Manager editor window event", ex);
                }
                finally
                {
                    _editorWindowEvent.Dispose();
                    _instanceMarker.ReleaseMutex();
                    _instanceMarker.Dispose();
                }
            }
        }
    }
}
