// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;

using CommunityToolkit.Mvvm.Messaging;

using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WorkspacesEditor.Messages;
using WorkspacesEditor.Telemetry;
using WorkspacesEditor.Utils;
using WorkspacesEditor.ViewModels;

namespace WorkspacesEditor
{
    public partial class App : Application, IDisposable
    {
        private MainWindow _mainWindow;
        private EventWaitHandle _moduleShutdownEvent;
        private RegisteredWaitHandle _moduleShutdownWait;
        private EventWaitHandle _moduleActivationEvent;
        private bool _isDisposed;
        private bool _shuttingDown;

        public static DispatcherQueue DispatcherQueue { get; private set; }

        public static WorkspacesEditorIO WorkspacesEditorIO { get; private set; }

        public static MainViewModel MainViewModel { get; private set; }

        public App()
        {
            PowerToysTelemetry.Log.WriteEvent(new WorkspacesEditorStartEvent() { TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });

            string languageTag = LanguageHelper.LoadLanguage();
            if (!string.IsNullOrEmpty(languageTag))
            {
                try
                {
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

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            if (_shuttingDown)
            {
                return;
            }

            DispatcherQueue = DispatcherQueue.GetForCurrentThread();

            WorkspacesEditorIO = new WorkspacesEditorIO();
            MainViewModel = new MainViewModel(WorkspacesEditorIO);
            MainViewModel.Initialize();

            _mainWindow = new MainWindow(_moduleActivationEvent);
            _mainWindow.Activate();

            StrongReferenceMessenger.Default.Register<CloseApplicationMessage>(this, (r, m) =>
            {
                Logger.LogInfo("CloseApplicationMessage received. Shutting down.");
                ((App)r).Shutdown();
            });
        }

        internal void ListenForModuleRequests(string eventName, DispatcherQueue dispatcherQueue)
        {
            try
            {
                if (EventWaitHandle.TryOpenExisting(eventName, out _moduleShutdownEvent))
                {
                    _moduleShutdownWait = ThreadPool.RegisterWaitForSingleObject(
                        _moduleShutdownEvent,
                        (_, _) => dispatcherQueue.TryEnqueue(Shutdown),
                        null,
                        Timeout.Infinite,
                        executeOnlyOnce: true);
                    _moduleActivationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, eventName + "-activate");
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                Logger.LogError("Failed to register Workspaces Editor activation and shutdown events.", ex);
            }
        }

        internal void Shutdown()
        {
            if (_shuttingDown)
            {
                return;
            }

            _shuttingDown = true;
            var window = _mainWindow;
            _mainWindow = null;

            try
            {
                window?.CloseForShutdown();
            }
            finally
            {
                try
                {
                    Dispose();
                }
                finally
                {
                    Exit();
                }
            }
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
                _moduleShutdownWait?.Unregister(null);
                _moduleShutdownEvent?.Dispose();
                _moduleActivationEvent?.Dispose();
                StrongReferenceMessenger.Default.UnregisterAll(this);
                MainViewModel?.Dispose();
            }

            GC.SuppressFinalize(this);
        }
    }
}
