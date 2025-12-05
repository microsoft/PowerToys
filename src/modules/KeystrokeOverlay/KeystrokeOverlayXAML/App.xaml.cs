// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using KeystrokeOverlayUI.Services;
using ManagedCommon;
using Microsoft.UI.Xaml;

namespace KeystrokeOverlayUI
{
    /// <summary>
    /// Application entry point for the Keystroke Overlay UI.
    /// </summary>
    public sealed partial class App : Application, IDisposable
    {
        private readonly ProcessJob _job = new();
        private Window _window;
        private bool _disposed;

        /// <summary>
        /// Gets the running native keystroke server process.
        /// </summary>
        public Process KeystrokeServerProcess { get; private set; }

        public App()
        {
            InitializeComponent();

            string appLanguage = LanguageHelper.LoadLanguage();
            if (!string.IsNullOrEmpty(appLanguage))
            {
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = appLanguage;
            }

            Logger.InitializeLogger("\\KeystrokeOverlay\\Logs");

            // Explicitly use Microsoft.UI.Xaml.UnhandledExceptionEventArgs
            UnhandledException += App_UnhandledException;
        }

        /// <inheritdoc/>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            StartKeystrokeServer();

            _window = new MainWindow();
            _window.Activate();
        }

        private void StartKeystrokeServer()
        {
            try
            {
                string exePath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "PowerToys.KeystrokeOverlayKeystrokeServer.exe");

                if (!File.Exists(exePath))
                {
                    Logger.LogError($"Keystroke server missing: {exePath}");
                    return;
                }

                KeystrokeServerProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    },
                };

                KeystrokeServerProcess.Start();

                // Add process to job so it dies with UI
                _job.AddProcess(KeystrokeServerProcess.Handle);

                Logger.LogInfo("Keystroke server started and assigned to job object.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to launch keystroke server.", ex);
            }
        }

        // FIXED: Explicit namespace to resolve ambiguity
        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logger.LogError("Unhandled UI exception.", e.Exception);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!_disposed)
            {
                _job.Dispose();
                _disposed = true;
            }
        }
    }
}
