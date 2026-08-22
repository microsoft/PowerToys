// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.Win32;
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

            if (_mainViewModel == null)
            {
                _mainViewModel = new MainViewModel(WorkspacesEditorIO);
            }

            // Fast, non-blocking initial load so the window appears immediately
            // (reads via the service if it is already up, otherwise the legacy
            // file).  Dialogs suppressed here so a transient upgrade-time rejection
            // doesn't pop before the background provisioning has had a chance to
            // heal it.  First-run provisioning — UAC + MSIX deploy, which is
            // serialized machine-wide and can queue behind a PowerToys upgrade —
            // runs OFF the UI thread below so the editor never appears hung.
            WorkspacesEditorIO.ParseWorkspaces(_mainViewModel, runBootstrap: false, showDialogs: false);

            // If the fast load already produced workspaces, the service was up and
            // provisioning is a no-op — a later reload would only risk clobbering
            // the user's edits, so we suppress it (see the guard below).
            bool initialListEmpty = _mainViewModel.Workspaces.Count == 0;

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

            // Deferred per-user service provisioning + legacy migration, OFF the UI
            // thread.  When it completes we reload ONLY when it is safe to do so —
            // never while the user is mid-edit, and only when the initial load was
            // empty (so the reload can add newly migrated/protected workspaces but
            // can never destroy user work).  IsProvisioning drives an optional
            // "Setting up protection…" affordance in the UI.
            _mainViewModel.IsProvisioning = true;
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    // Provision the per-user service + migrate legacy data.  The
                    // reload below reads the result from the protected store (or
                    // surfaces the "set up protection" message); we no longer branch
                    // on the return value here.
                    WorkspacesEditorIO.EnsureSettingsProvisioned();
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Background settings provisioning failed: {ex.Message}");
                }

                Dispatcher.Invoke(() =>
                {
                    _mainViewModel.IsProvisioning = false;

                    // Reload from the protected store once provisioning settled,
                    // guarded so it never clobbers in-progress edits or an
                    // already-populated list.  We reload even when the service is
                    // NOT available: with no unprotected fallback, that path now
                    // surfaces the "set up protection" message instead of leaving a
                    // silently-empty editor.  When the service IS available it loads
                    // the (possibly just-migrated) protected workspaces.
                    if (initialListEmpty && !_mainViewModel.IsEditInProgress)
                    {
                        WorkspacesEditorIO.ParseWorkspaces(_mainViewModel, runBootstrap: false, showDialogs: true);
                    }
                });
            });
        }

        public static Theme GetCurrentTheme()
        {
            if (SystemParameters.HighContrast)
            {
                return Theme.HighContrastOne;
            }

            try
            {
                var useLightTheme = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme",
                    1);
                return (useLightTheme is int value && value == 0) ? Theme.Dark : Theme.Light;
            }
            catch
            {
                return Theme.Light;
            }
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
