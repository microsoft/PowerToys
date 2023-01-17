// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using interop;
using ManagedCommon;
using PowerAccent.Core.Tools;

namespace PowerAccent.UI;

internal static class Program
{
    private static readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
    private static App _application;
    private static int _powerToysRunnerPid;

    [STAThread]
    public static void Main(string[] args)
    {
        if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredQuickAccentEnabledValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
        {
            Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
            return;
        }

        Arguments(args);

        InitEvents();

        _application = new App();
        _application.InitializeComponent();
        _application.Run();
    }

    private static void InitEvents()
    {
        Task.Run(
            () =>
            {
                EventWaitHandle eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.PowerAccentExitEvent());
                if (eventHandle.WaitOne())
                {
                    Terminate();
                }
            },
            _tokenSource.Token);
    }

    private static void Arguments(string[] args)
    {
        if (args?.Length > 0)
        {
            try
            {
                if (int.TryParse(args[0], out _powerToysRunnerPid))
                {
                    Logger.LogInfo($"QuickAccent started from the PowerToys Runner. Runner pid={_powerToysRunnerPid}");

                    RunnerHelper.WaitForPowerToysRunner(_powerToysRunnerPid, () =>
                    {
                        Logger.LogInfo("PowerToys Runner exited. Exiting QuickAccent");
                        Terminate();
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
        else
        {
            Logger.LogInfo($"QuickAccent started detached from PowerToys Runner.");
            _powerToysRunnerPid = -1;
        }
    }

    private static void Terminate()
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _tokenSource.Cancel();
            Application.Current.Shutdown();
        });
    }
}
