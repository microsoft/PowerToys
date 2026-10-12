// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Threading;
using System.Windows;

using ManagedCommon;
using PowerToys.Interop;
using WorkspacesLauncherUI.ViewModels;

namespace WorkspacesLauncherUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, IDisposable
    {
        private static Mutex _instanceMutex;

        // Create an instance of the  IPC wrapper.
        private static TwoWayPipeMessageIPCManaged ipcmanager;

        private StatusWindow _mainWindow;

        private MainViewModel _mainViewModel;

        private bool _isDisposed;

        public static Action<string> IPCMessageReceivedCallback { get; set; }

        public App()
        {
        }

        public static void SendIPCMessage(string message)
        {
            if (ipcmanager != null)
            {
                ipcmanager.Send(message);
            }
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            Logger.InitializeLogger("\\Workspaces\\WorkspacesLauncherUI");
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            var languageTag = LanguageHelper.LoadLanguage();

            if (!string.IsNullOrEmpty(languageTag))
            {
                try
                {
                    System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo(languageTag);
                }
                catch (CultureNotFoundException ex)
                {
                    Logger.LogError("CultureNotFoundException: " + ex.Message);
                }
            }

            var confirmationOnly = e.Args.Length != 0;
            var operationId = string.Empty;
            int ownerPid = 0;
            if (confirmationOnly)
            {
                if (e.Args.Length != 3 || e.Args[0] != "--confirmation-only" ||
                    !Guid.TryParse(e.Args[1], out var id) || id == Guid.Empty ||
                    !int.TryParse(e.Args[2], NumberStyles.None, CultureInfo.InvariantCulture, out ownerPid) || ownerPid <= 0)
                {
                    Logger.LogError("Invalid Workspaces confirmation host arguments.");
                    Shutdown(1);
                    return;
                }

                operationId = id.ToString("D");
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                if (!RunnerHelper.TryWaitForProcessExit(ownerPid, () =>
                {
                    if (!Dispatcher.HasShutdownStarted)
                    {
                        Dispatcher.BeginInvoke(new Action(() => Shutdown()));
                    }
                }))
                {
                    Logger.LogError("Workspaces confirmation owner is unavailable.");
                    Shutdown(1);
                    return;
                }
            }

            var appName = "Local\\PowerToys_Workspaces_LauncherUI_InstanceMutex" + operationId;
            bool createdNew;
            _instanceMutex = new Mutex(true, appName, out createdNew);
            if (!createdNew)
            {
                Logger.LogWarning("Another instance of Workspaces Launcher UI is already running. Exiting this instance.");
                _instanceMutex = null;
                Shutdown(0);
                return;
            }

            if (PowerToys.GPOWrapperProjection.GPOWrapper.GetConfiguredWorkspacesEnabledValue() == PowerToys.GPOWrapperProjection.GpoRuleConfigured.Disabled)
            {
                Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
                Shutdown(0);
                return;
            }

            _mainViewModel = new MainViewModel(confirmationOnly);
            ipcmanager = new TwoWayPipeMessageIPCManaged("\\\\.\\pipe\\powertoys_workspaces_ui_" + operationId, "\\\\.\\pipe\\powertoys_workspaces_launcher_ui_" + operationId, (string message) =>
            {
                if (message.Length > 0)
                {
                    IPCMessageReceivedCallback?.Invoke(message);
                }
            });
            ipcmanager.Start();

            if (confirmationOnly)
            {
                return;
            }

            // normal start of editor
            if (_mainWindow == null)
            {
                _mainWindow = new StatusWindow(_mainViewModel);
            }

            // reset main window owner to keep it on the top
            _mainWindow.ShowActivated = true;
            _mainWindow.Topmost = true;
            _mainWindow.Show();
        }

        private void OnExit(object sender, ExitEventArgs e)
        {
            if (_instanceMutex != null)
            {
                _instanceMutex.ReleaseMutex();
            }

            Dispose();
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            Logger.LogError("Unhandled exception occurred", args.ExceptionObject as Exception);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _mainViewModel?.Dispose();
                    ipcmanager?.End();
                    ipcmanager?.Dispose();

                    _instanceMutex?.Dispose();
                }

                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
