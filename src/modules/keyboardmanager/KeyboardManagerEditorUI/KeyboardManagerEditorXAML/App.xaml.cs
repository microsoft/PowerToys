// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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

            // Before anything touches the configuration: a second instance would race the first one
            // on default.json and editorSettings.json.
            if (!SingleInstanceGuard.TryAcquire())
            {
                Logger.LogInfo("Another Keyboard Manager editor is already running, activating it and exiting");
                SingleInstanceGuard.ActivateExistingInstance();
                Environment.Exit(0);
            }

            this.InitializeComponent();

            UnhandledException += App_UnhandledException;

            // Stop the engine from applying the existing remappings while the editor is open, so
            // recording a trigger captures the physical key rather than what it is remapped to.
            // Released in MainWindow_Closed. The classic editor does the same via EventLocker.
            EditorWindowEventLock.Acquire();

            // Backstop for exit paths that do not go through MainWindow_Closed: the event is
            // manual-reset and outlives this process, so leaving it set would disable the engine.
            AppDomain.CurrentDomain.ProcessExit += (_, _) => EditorWindowEventLock.Release();

            SettingsManager.CorrelateServiceAndEditorMappings();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
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

            Logger.LogInfo("keyboard-manager WinUI3 editor window is launched");

            // Close with whichever launcher started us, so an orphaned editor cannot hold the
            // engine-suspend event set for the rest of the session.
            ParentProcessWatcher.CloseWhenParentExits(
                () => MainWindow.DispatcherQueue.TryEnqueue(() => MainWindow.Close()));
        }

        /// <summary>
        /// Log the unhandled exception for the editor.
        /// </summary>
        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logger.LogError("Unhandled exception", e.Exception);

            // This handler leaves e.Handled false, so the process is about to go down. Leaving the
            // suspend event set would keep the engine disabled until it is restarted.
            EditorWindowEventLock.Release();
        }

        internal static MainWindow MainWindow { get; private set; } = null!;
    }
}
