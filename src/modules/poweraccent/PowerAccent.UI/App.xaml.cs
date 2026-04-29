// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace PowerAccent.UI
{
    public partial class App : Application, IDisposable
    {
        private readonly ETWTrace _etwTrace = new ETWTrace();

        private bool _disposed;
        private Selector _window;

        public static Window Window { get; private set; }

        public App()
        {
            try
            {
                string appLanguage = LanguageHelper.LoadLanguage();
                if (!string.IsNullOrEmpty(appLanguage))
                {
                    Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = appLanguage;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Language initialization error: " + ex.Message);
            }

            InitializeComponent();

            UnhandledException += App_UnhandledException;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // The keyboard hook callbacks marshal onto this dispatcher before touching UI.
            // Wire it up before the Selector spins up its hook so no hook event runs on a worker thread.
            var dispatcher = DispatcherQueue.GetForCurrentThread();
            Core.PowerAccent.DispatcherInvoker = action => dispatcher.TryEnqueue(() => action());

            _window = new Selector();
            Window = _window;
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logger.LogError("Unhandled exception", e.Exception);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _etwTrace?.Dispose();
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
