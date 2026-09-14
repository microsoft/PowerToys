// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ManagedCommon;
using WorkspacesLauncherUI.IPC;
using WorkspacesLauncherUI.ViewModels;

namespace WorkspacesLauncherUI
{
    public partial class App : Application, IDisposable
    {
        private Mutex _instanceMutex;
        private LauncherConnection _connection;
        private StatusWindow _mainWindow;
        private MainViewModel _mainViewModel;
        private int _exitCode;
        private bool _ownsMutex;
        private bool _shuttingDown;
        private bool _isDisposed;

        private async void OnStartup(object sender, StartupEventArgs e)
        {
            Logger.InitializeLogger("\\Workspaces\\WorkspacesLauncherUI");
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var isPreview = false;
#if DEBUG
            isPreview = e.Args.Length == 2 && e.Args[0] == "--preview-signature-warning";
#else
            if (Array.IndexOf(e.Args, "--preview-signature-warning") >= 0)
            {
                Logger.LogError("Workspaces warning preview is only available in Debug builds.");
                RequestShutdown(1);
                return;
            }
#endif

            var launcherProcessId = 0;
            string pipeName = null;
            if (!isPreview && !LauncherConnection.TryParseArguments(e.Args, out launcherProcessId, out pipeName))
            {
                Logger.LogError("Workspaces Launcher UI requires valid authenticated-launch arguments.");
                RequestShutdown(1);
                return;
            }

            var languageTag = LanguageHelper.LoadLanguage();
            if (!string.IsNullOrEmpty(languageTag))
            {
                try
                {
                    Thread.CurrentThread.CurrentUICulture = new CultureInfo(languageTag);
                }
                catch (CultureNotFoundException exception)
                {
                    LauncherConnection.LogFailure("Unable to apply the Workspaces UI language", exception);
                }
            }

            if (PowerToys.GPOWrapperProjection.GPOWrapper.GetConfiguredWorkspacesEnabledValue() == PowerToys.GPOWrapperProjection.GpoRuleConfigured.Disabled)
            {
                Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
                RequestShutdown();
                return;
            }

#if DEBUG
            if (isPreview)
            {
                var preview = new SignatureWarningWindow(new Models.SignatureWarningRequest
                {
                    RequestId = Guid.NewGuid().ToString("B"),
                    AppName = "PowerToys.WorkspacesLauncherUI",
                    Path = Environment.ProcessPath ?? string.Empty,
                    Arguments = string.Empty,
                    Reason = e.Args[1],
                    Status = WorkspacesLauncherUI.Properties.Resources.SignatureWarningPreviewStatus,
                })
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ShowInTaskbar = true,
                };
                preview.EnableRunChoice();
                preview.ShowDialog();
                RequestShutdown();
                return;
            }
#endif

            try
            {
                _instanceMutex = new Mutex(true, "Local\\PowerToys_Workspaces_LauncherUI_InstanceMutex", out _ownsMutex);
                if (!_ownsMutex)
                {
                    Logger.LogWarning("Another instance of Workspaces Launcher UI is already running.");
                    RequestShutdown(1);
                    return;
                }

                _connection = new LauncherConnection(launcherProcessId, pipeName);
                _connection.Closed += ScheduleShutdown;

                await _connection.ConnectAsync();
                if (_shuttingDown || !_connection.IsOpen)
                {
                    RequestShutdown();
                    return;
                }

                _mainViewModel = new MainViewModel(_connection);
                _mainWindow = new StatusWindow(_mainViewModel);
                MainWindow = _mainWindow;
                _mainWindow.Closed += (_, _) => RequestShutdown();
                _mainWindow.ShowActivated = true;
                _mainWindow.Topmost = true;
                _mainWindow.Show();
                _connection.StartReceiving(ScheduleMessage);
                if (!await _connection.SendReadyAsync())
                {
                    RequestShutdown();
                }
            }
            catch (Exception exception)
            {
                LauncherConnection.LogFailure("Unable to start Workspaces Launcher UI", exception);
                RequestShutdown(1);
            }
        }

        private void ScheduleMessage(LauncherMessage message)
        {
            if (!Dispatcher.HasShutdownStarted)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() =>
                {
                    if (_shuttingDown)
                    {
                        return;
                    }

                    try
                    {
                        if (message.Type == "shutdown")
                        {
                            RequestShutdown();
                        }
                        else
                        {
                            _mainViewModel.HandleMessage(message);
                        }
                    }
                    catch (Exception exception)
                    {
                        _connection.Fail("Unable to handle a Workspaces launcher message", exception);
                    }
                }));
            }
        }

        private void ScheduleShutdown()
        {
            if (!Dispatcher.HasShutdownStarted)
            {
                // Always enqueue, even on the UI thread: a failing send must unwind before disposal.
                Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => RequestShutdown()));
            }
        }

        private void RequestShutdown(int exitCode = 0)
        {
            _exitCode = Math.Max(_exitCode, Math.Max(exitCode, _connection?.HasFailed == true ? 1 : 0));
            if (_shuttingDown)
            {
                return;
            }

            _shuttingDown = true;
            _mainViewModel?.Dispose();
            _connection?.Close();
            Shutdown(_exitCode);
        }

        private void OnExit(object sender, ExitEventArgs e)
        {
            Dispose();

            // Window closure and disposal callbacks must not turn a failed launch into success.
            if (e.ApplicationExitCode == 0)
            {
                e.ApplicationExitCode = Math.Max(_exitCode, _connection?.HasFailed == true ? 1 : 0);
            }

            Logger.LogInfo($"Workspaces Launcher UI exiting with code {e.ApplicationExitCode}.");
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            if (args.ExceptionObject is Exception exception)
            {
                LauncherConnection.LogFailure("Unhandled Workspaces Launcher UI exception", exception);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _mainViewModel?.Dispose();
                    if (_connection != null)
                    {
                        _connection.Closed -= ScheduleShutdown;
                        _connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    }

                    if (_ownsMutex)
                    {
                        _instanceMutex.ReleaseMutex();
                        _ownsMutex = false;
                    }

                    _instanceMutex?.Dispose();
                    AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
                }

                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
