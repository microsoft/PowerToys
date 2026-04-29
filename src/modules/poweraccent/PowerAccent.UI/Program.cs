// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using PowerToys.Interop;

namespace PowerAccent.UI;

internal static class Program
{
    private static readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
    private static int _powerToysRunnerPid;

    /// <summary>
    /// UI-thread dispatcher captured at startup so worker threads (the runner-exit watcher and the
    /// PowerAccentExitEvent watcher) can post a clean shutdown back onto the UI thread.
    /// </summary>
    public static DispatcherQueue UIDispatcher { get; set; }

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

        // Single-instance gate. AppInstance.FindOrRegisterForKey is the WinUI 3 equivalent of
        // the named-mutex check the WPF version used.
        var instanceKey = AppInstance.FindOrRegisterForKey("PowerToys_QuickAccent_Instance");
        if (!instanceKey.IsCurrent)
        {
            Logger.LogWarning("Another running QuickAccent instance was detected. Exiting QuickAccent");
            return;
        }

        Arguments(args);
        InitEvents();

        Microsoft.UI.Xaml.Application.Start((p) =>
        {
            var dispatcher = DispatcherQueue.GetForCurrentThread();
            UIDispatcher = dispatcher;
            var context = new DispatcherQueueSynchronizationContext(dispatcher);
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
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
        _tokenSource.Cancel();

        var dispatcher = UIDispatcher;
        if (dispatcher != null && dispatcher.TryEnqueue(() => Microsoft.UI.Xaml.Application.Current?.Exit()))
        {
            return;
        }

        // App hasn't started yet (early termination) — fall back to a hard exit.
        Environment.Exit(0);
    }
}
