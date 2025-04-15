// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using Common.UI;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using WorkspacesEditor.Telemetry;
using WorkspacesEditor.Utils;
using WorkspacesEditor.ViewModels;

namespace WorkspacesEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, IDisposable
    {
        private static Mutex _instanceMutex;

        public static WorkspacesEditorIO WorkspacesEditorIO { get; private set; }

        private MainWindow _mainWindow;

        private MainViewModel _mainViewModel;

        public static ThemeManager ThemeManager { get; set; }

        private bool _isDisposed;

        private ETWTrace etwTrace = new ETWTrace();

        public App()
        {
            PowerToysTelemetry.Log.WriteEvent(new WorkspacesEditorStartEvent() { TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });

            WorkspacesEditorIO = new WorkspacesEditorIO();
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            Logger.InitializeLogger("\\Workspaces\\Logs");
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

            const string appName = "Local\\PowerToys_Workspaces_Editor_InstanceMutex";
            bool createdNew;
            _instanceMutex = new Mutex(true, appName, out createdNew);
            if (!createdNew)
            {
                Logger.LogWarning("Another instance of Workspaces Editor is already running. Exiting this instance.");
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

            var args = e?.Args;
            int powerToysRunnerPid;
            if (args?.Length > 0)
            {
                _ = int.TryParse(args[0], out powerToysRunnerPid);

                Logger.LogInfo($"WorkspacesEditor started from the PowerToys Runner. Runner pid={powerToysRunnerPid}");
                RunnerHelper.WaitForPowerToysRunner(powerToysRunnerPid, () =>
                {
                    Logger.LogInfo("PowerToys Runner exited. Exiting WorkspacesEditor");
                    Dispatcher.Invoke(Shutdown);
                });
            }

            ThemeManager = new ThemeManager(this);

            if (_mainViewModel == null)
            {
                _mainViewModel = new MainViewModel(WorkspacesEditorIO);
            }

            var parseResult = WorkspacesEditorIO.ParseWorkspaces(_mainViewModel);

            // normal start of editor
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow(_mainViewModel);
            }

            // reset main window owner to keep it on the top
            _mainWindow.ShowActivated = true;
            _mainWindow.Topmost = true;
            _mainWindow.Show();

            // we can reset topmost flag after it's opened
            _mainWindow.Topmost = false;
        }

        private void OnExit(object sender, ExitEventArgs e)
        {
            if (_instanceMutex != null)
            {
                _instanceMutex.ReleaseMutex();
            }

            Dispose();
            Environment.Exit(0);
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
                    ThemeManager?.Dispose();
                    _instanceMutex?.Dispose();
                    etwTrace?.Dispose();
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
