﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Threading;
using System.Windows;

using ColorPicker.Mouse;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;

namespace ColorPickerUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, IDisposable
    {
        public ETWTrace EtwTrace { get; private set; } = new ETWTrace();

        private Mutex _instanceMutex;
        private static string[] _args;
        private int _powerToysRunnerPid;
        private bool disposedValue;

        private CancellationTokenSource NativeThreadCTS { get; set; }

        [Export]
        private static CancellationToken ExitToken { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                string appLanguage = LanguageHelper.LoadLanguage();
                if (!string.IsNullOrEmpty(appLanguage))
                {
                    System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo(appLanguage);
                }
            }
            catch (CultureNotFoundException ex)
            {
                Logger.LogError("CultureNotFoundException: " + ex.Message);
            }

            NativeThreadCTS = new CancellationTokenSource();
            ExitToken = NativeThreadCTS.Token;

            _args = e?.Args;

            // allow only one instance of color picker
            _instanceMutex = new Mutex(true, @"Local\PowerToys_ColorPicker_InstanceMutex", out bool createdNew);
            if (!createdNew)
            {
                Logger.LogWarning("There is ColorPicker instance running. Exiting Color Picker");
                _instanceMutex = null;
                Shutdown(0);
                return;
            }

            if (_args?.Length > 0)
            {
                _ = int.TryParse(_args[0], out _powerToysRunnerPid);

                Logger.LogInfo($"Color Picker started from the PowerToys Runner. Runner pid={_powerToysRunnerPid}");
                RunnerHelper.WaitForPowerToysRunner(_powerToysRunnerPid, () =>
                {
                    Logger.LogInfo("PowerToys Runner exited. Exiting ColorPicker");
                    NativeThreadCTS.Cancel();
                    Dispatcher.Invoke(Shutdown);
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

            CursorManager.RestoreOriginalCursors();
            base.OnExit(e);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _instanceMutex?.Dispose();
                    EtwTrace?.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public bool IsRunningDetachedFromPowerToys()
        {
            return _powerToysRunnerPid == -1;
        }
    }
}
