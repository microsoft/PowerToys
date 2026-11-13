// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;

namespace WorkspacesEditor
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Logger.InitializeLogger("\\Workspaces\\WorkspacesEditor");

            WinRT.ComWrappersSupport.InitializeComWrappers();

            if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredWorkspacesEnabledValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
                return;
            }

            const string mutexName = "Local\\PowerToys_Workspaces_Editor_InstanceMutex";
            bool createdNew;
            using var mutex = new Mutex(true, mutexName, out createdNew);

            if (!createdNew)
            {
                Logger.LogWarning("Another instance of Workspaces Editor is already running. Exiting this instance.");
                return;
            }

            // End the local recording session when the UI message loop exits, including Runner shutdown.
            using var etwTrace = new ETWTrace();
            Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
                var context = new DispatcherQueueSynchronizationContext(dispatcherQueue);
                SynchronizationContext.SetSynchronizationContext(context);
                var app = new App();

                if (args.Length > 0 && int.TryParse(args[0], out int powerToysRunnerPid))
                {
                    Logger.LogInfo($"WorkspacesEditor started from the PowerToys Runner. Runner pid={powerToysRunnerPid}");
                    if (!RunnerHelper.TryWaitForProcessExit(powerToysRunnerPid, () =>
                    {
                        Logger.LogInfo("PowerToys Runner exited. Exiting WorkspacesEditor.");
                        dispatcherQueue.TryEnqueue(app.Shutdown);
                    }))
                    {
                        dispatcherQueue.TryEnqueue(app.Shutdown);
                    }
                }
            });
        }
    }
}
