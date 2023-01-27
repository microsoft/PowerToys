// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Windows;
using ManagedCommon;
using PastePlain.Helpers;
using PastePlain.Keyboard;
using PastePlain.Settings;

namespace PastePlain;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application, IDisposable
{
    private KeyboardMonitor? keyboardMonitor;
    private Mutex? _instanceMutex;
    private int _powerToysRunnerPid;

    private CancellationTokenSource NativeThreadCTS { get; set; }

    public App()
    {
        NativeThreadCTS = new CancellationTokenSource();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        keyboardMonitor?.Dispose();
    }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        if (PowerToys.GPOWrapperProjection.GPOWrapper.GetConfiguredPastePlainEnabledValue() == PowerToys.GPOWrapperProjection.GpoRuleConfigured.Disabled)
        {
            Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
            Shutdown();
            return;
        }

        // allow only one instance of PastePlain
        _instanceMutex = new Mutex(true, @"Local\PowerToys_PastePlain_InstanceMutex", out bool createdNew);
        if (!createdNew)
        {
            Logger.LogWarning("Another running PastePlain instance was detected. Exiting PastePlain");
            _instanceMutex = null;
            Shutdown();
            return;
        }

        if (e.Args?.Length > 0)
        {
            try
            {
                _ = int.TryParse(e.Args[0], out _powerToysRunnerPid);
                Logger.LogInfo($"PastePlain started from the PowerToys Runner. Runner pid={_powerToysRunnerPid}");

                RunnerHelper.WaitForPowerToysRunner(_powerToysRunnerPid, () =>
                {
                    Logger.LogInfo("PowerToys Runner exited. Exiting PastePlain");
                    NativeThreadCTS.Cancel();
                    Application.Current.Dispatcher.Invoke(() => Shutdown());
                });
                var userSettings = new UserSettings(new Helpers.ThrottledActionInvoker());
            }
            catch (Exception ex)
            {
                Logger.LogError($"PastePlain got an exception on start: {ex}");
            }
        }
        else
        {
            Logger.LogInfo($"PastePlain started detached from PowerToys Runner.");
            _powerToysRunnerPid = -1;
            var userSettings = new UserSettings(new Helpers.ThrottledActionInvoker());
            keyboardMonitor = new KeyboardMonitor(userSettings);
            keyboardMonitor?.Start();
        }
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
