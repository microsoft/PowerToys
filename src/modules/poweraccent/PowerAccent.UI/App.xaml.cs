// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Windows;
using Common.UI;

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
            const string appName = "QuickAccent";

            _mutex = new Mutex(true, appName, out bool createdNew);

            if (!createdNew)
            {
                // app is already running! Exiting the application
                Application.Current.Shutdown();
            }

            _themeManager = new ThemeManager(this);
            base.OnStartup(e);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
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
