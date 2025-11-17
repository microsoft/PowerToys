// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Windows;

using ManagedCommon;
using Microsoft.PowerToys.Telemetry;

namespace PowerAccent.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, IDisposable
    {
        private static Mutex _mutex;
        private bool _disposed;
        private ETWTrace _etwTrace = new ETWTrace();

        protected override void OnStartup(StartupEventArgs e)
        {
            _mutex = new Mutex(true, "QuickAccent", out bool createdNew);

            if (!createdNew)
            {
                Logger.LogWarning("Another running QuickAccent instance was detected. Exiting QuickAccent");
                Application.Current.Shutdown();
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            base.OnExit(e);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _mutex?.Dispose();
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
