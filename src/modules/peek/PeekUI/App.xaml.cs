using ManagedCommon;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PeekUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, IDisposable
    {
        private Mutex? _instanceMutex;
        private static string[] _args = Array.Empty<string>();
        private int _powerToysRunnerPid;
        private bool disposedValue;

        protected override void OnStartup(StartupEventArgs e)
        {
            _args = e?.Args ?? Array.Empty<string>();

            // allow only one instance of color picker
            _instanceMutex = new Mutex(true, @"Local\PowerToys_Peek_InstanceMutex", out bool createdNew);
            if (!createdNew)
            {
                _instanceMutex = null;
                Environment.Exit(0);
                return;
            }

            while (!Debugger.IsAttached)
            {
                Thread.Sleep(100);
            }

            if (_args?.Length > 0)
            {
                _ = int.TryParse(_args[0], out _powerToysRunnerPid);

                RunnerHelper.WaitForPowerToysRunner(_powerToysRunnerPid, () =>
                {
                    Environment.Exit(0);
                });
            }
            else
            {
                _powerToysRunnerPid = -1;
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_instanceMutex != null)
            {
                _instanceMutex.ReleaseMutex();
            }

            base.OnExit(e);
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
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public bool IsRunningDetachedFromPowerToys()
        {
            return _powerToysRunnerPid == -1;
        }
    }
}
