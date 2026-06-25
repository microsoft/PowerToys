// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using ManagedCommon;
using Microsoft.UI.Dispatching;
using PowerToys.Interop;

namespace PowerAccent.UI;

internal static class Program
{
    private static readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
    private static Mutex _mutex;
    private static int _powerToysRunnerPid;

    [STAThread]
    public static void Main(string[] args)
    {
        Logger.InitializeLogger("\\QuickAccent\\Logs");
        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredQuickAccentEnabledValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
        {
            Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
            return;
        }

        _mutex = new Mutex(true, "QuickAccent", out bool createdNew);
        if (!createdNew)
        {
            Logger.LogWarning("Another running QuickAccent instance was detected. Exiting QuickAccent");
            return;
        }

        Arguments(args);
        InitExitListener();

        Microsoft.UI.Xaml.Application.Start((p) =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });

        _mutex?.ReleaseMutex();
    }

    private static void InitExitListener()
    {
        Task.Run(
            () =>
            {
                using EventWaitHandle eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.PowerAccentExitEvent());
                if (eventHandle.WaitOne())
                {
                    Terminate();
                }
            },
            _tokenSource.Token);
    }

    private static void Arguments(string[] args)
    {
        if (args?.Length > 0 && int.TryParse(args[0], out _powerToysRunnerPid))
        {
            Logger.LogInfo($"QuickAccent started from the PowerToys Runner. Runner pid={_powerToysRunnerPid}");
            RunnerHelper.WaitForPowerToysRunner(_powerToysRunnerPid, () =>
            {
                Logger.LogInfo("PowerToys Runner exited. Exiting QuickAccent");
                Terminate();
            });
        }
        else
        {
            Logger.LogInfo("QuickAccent started detached from PowerToys Runner.");
            _powerToysRunnerPid = -1;
        }
    }

    private static void Terminate()
    {
        var app = App.Current;
        app?.DispatcherQueueForApp?.TryEnqueue(() =>
        {
            _tokenSource.Cancel();
            app.Dispose();   // disposes ETWTrace (idempotent via _disposed guard)
            app.Exit();
        });
    }
}
