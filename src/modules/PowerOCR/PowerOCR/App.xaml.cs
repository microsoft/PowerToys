// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Windows;
using ManagedCommon;
using PowerOCR.Keyboard;
using PowerOCR.Settings;

namespace PowerOCR;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application, IDisposable
{
    private KeyboardMonitor? keyboardMonitor;
    private Mutex? _instanceMutex;
    private static string[] _args = Array.Empty<string>();
    private int _powerToysRunnerPid;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        keyboardMonitor?.Dispose();
    }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // allow only one instance of PowerOCR
        _instanceMutex = new Mutex(true, @"Local\PowerToys_PowerOCR_InstanceMutex", out bool createdNew);
        if (!createdNew)
        {
            _instanceMutex = null;
            Environment.Exit(0);
            return;
        }

        if (e.Args?.Length > 0)
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

        var userSettings = new UserSettings();
        keyboardMonitor = new KeyboardMonitor(userSettings);
        keyboardMonitor?.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_instanceMutex != null)
        {
            _instanceMutex.ReleaseMutex();
        }

        base.OnExit(e);
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        Dispose();
    }
}
