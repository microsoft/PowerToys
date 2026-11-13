// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PowerToys.Interop;
using WorkspacesLauncherUI.ViewModels;

namespace WorkspacesLauncherUI
{
    /// <summary>
    /// WinUI 3 Application class for the Workspaces Launcher UI.
    /// Manages the IPC pipe connection to the C++ launcher engine and hosts the status window.
    /// </summary>
    public partial class App : Application, IDisposable
    {
        private readonly LauncherSession _session;
        private StatusWindow _mainWindow;
        private TwoWayPipeMessageIPCManaged _ipcManager;
        private MainViewModel _mainViewModel;
        private Window _lifetimeWindow;
        private bool _isDisposed;
        private bool _shuttingDown;

        internal StatusWindow MainWindow => _mainWindow;

        public static Action<string> IPCMessageReceivedCallback { get; set; }

        public static DispatcherQueue DispatcherQueue { get; private set; }

        internal App(LauncherSession session)
        {
            _session = session;
            string languageTag = LanguageHelper.LoadLanguage();
            if (!string.IsNullOrEmpty(languageTag))
            {
                try
                {
                    CultureInfo.CurrentUICulture = new CultureInfo(languageTag);
                    Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = languageTag;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to set language override: " + ex.Message);
                }
            }

            this.InitializeComponent();
            this.UnhandledException += OnUnhandledException;
        }

        public static void SendIPCMessage(string message)
        {
            if ((Current as App)?._ipcManager != null)
            {
                (Current as App)._ipcManager.Send(message);
            }
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

            if (_session.OwnerPid != 0 && !RunnerHelper.TryWaitForProcessExit(_session.OwnerPid, () => DispatcherQueue.TryEnqueue(Shutdown)))
            {
                Logger.LogError("Workspaces confirmation owner is unavailable.");
                Environment.ExitCode = 1;
                Shutdown();
                return;
            }

            _mainViewModel = new MainViewModel(_session.ConfirmationOnly, () => _mainWindow?.AppWindow.IsVisible == true, Shutdown);
            if (_session.ConfirmationOnly)
            {
                // Keep the host alive between prompts without displaying the launch-status UI.
                _lifetimeWindow = new Window();
                _lifetimeWindow.AppWindow.Hide();
            }

            _ipcManager = new TwoWayPipeMessageIPCManaged(
                "\\\\.\\pipe\\powertoys_workspaces_ui_" + _session.OperationId,
                "\\\\.\\pipe\\powertoys_workspaces_launcher_ui_" + _session.OperationId,
                (string message) =>
                {
                    if (IPCMessageReceivedCallback != null && message.Length > 0)
                    {
                        IPCMessageReceivedCallback?.Invoke(message);
                    }
                });
            _ipcManager.Start();

            if (!_session.ConfirmationOnly)
            {
                _mainWindow = new StatusWindow(_mainViewModel);
                _mainWindow.Activate();
            }
        }

        internal void Shutdown()
        {
            if (_shuttingDown)
            {
                return;
            }

            _shuttingDown = true;
            Dispose();
            var window = _mainWindow;
            _mainWindow = null;
            window?.Close();
            _lifetimeWindow?.Close();
            _lifetimeWindow = null;
            Exit();
        }

        internal void OnMainWindowClosed()
        {
            _mainWindow = null;
            Shutdown();
        }

        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logger.LogError("Unhandled exception occurred", e.Exception);
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                _mainViewModel?.Dispose();
                _ipcManager?.End();
                _ipcManager?.Dispose();
            }

            GC.SuppressFinalize(this);
        }
    }
}
