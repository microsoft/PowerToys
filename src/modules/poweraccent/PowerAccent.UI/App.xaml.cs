// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Windows;
using Common.UI;
using PowerAccent.Core.Tools;

namespace PowerAccent.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, IDisposable
    {
        private static Mutex _mutex;
        private bool disposedValue;
        private ThemeManager _themeManager;

        protected override void OnStartup(StartupEventArgs e)
        {
            _mutex = new Mutex(true, "QuickAccent", out bool createdNew);

            if (!createdNew)
            {
                Logger.LogWarning("Another running QuickAccent instance was detected. Exiting QuickAccent");
                Application.Current.Shutdown();
            }

            _themeManager = new ThemeManager(this);
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            base.OnExit(e);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                _mutex?.Dispose();
                _themeManager?.Dispose();

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
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
