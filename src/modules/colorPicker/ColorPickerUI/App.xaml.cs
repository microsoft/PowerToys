// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Windows;
using ColorPicker.Helpers;
using ColorPicker.Mouse;
using ManagedCommon;

namespace ColorPickerUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, IDisposable
    {
        private Mutex _instanceMutex;
        private static string[] _args;
        private int _powerToysPid;
        private bool disposedValue;

        [STAThread]
        public static void Main(string[] args)
        {
            _args = args;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            try
            {
                using (var application = new App())
                {
                    application.InitializeComponent();
                    application.Run();
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Logger.LogError("Unhandled exception", ex);
                CursorManager.RestoreOriginalCursors();
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // allow only one instance of color picker
            _instanceMutex = new Mutex(true, @"Global\ColorPicker", out bool createdNew);
            if (!createdNew)
            {
                _instanceMutex = null;
                Application.Current.Shutdown();
                return;
            }

            if (_args.Length > 0)
            {
                _ = int.TryParse(_args[0], out _powerToysPid);
            }

            RunnerHelper.WaitForPowerToysRunner(_powerToysPid, () =>
            {
                Environment.Exit(0);
            });

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_instanceMutex != null)
            {
                _instanceMutex.ReleaseMutex();
            }

            CursorManager.RestoreOriginalCursors();
            base.OnExit(e);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.LogError("Unhandled exception", (e.ExceptionObject is Exception) ? (e.ExceptionObject as Exception) : new Exception());
            CursorManager.RestoreOriginalCursors();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _instanceMutex?.Dispose();
                }

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
