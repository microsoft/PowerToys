// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

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
        private bool _isDisposed;

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
            DispatcherQueue = DispatcherQueue.GetForCurrentThread();

            WorkspacesEditorIO = new WorkspacesEditorIO();
            MainViewModel = new MainViewModel(WorkspacesEditorIO);
            WorkspacesEditorIO.ParseWorkspaces(MainViewModel);
            MainViewModel.Initialize();

            _mainWindow = new MainWindow();
            _mainWindow.Activate();

            StrongReferenceMessenger.Default.Register<CloseApplicationMessage>(this, (r, m) =>
            {
                Logger.LogInfo("CloseApplicationMessage received. Shutting down.");
                ((App)r).Exit();
            });
        }

        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logger.LogError("Unhandled exception occurred", e.Exception);
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                MainViewModel?.Dispose();
                _isDisposed = true;
            }

            GC.SuppressFinalize(this);
        }
    }
}
