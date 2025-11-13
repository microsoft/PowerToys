// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;

namespace PowerDisplay
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Logger.InitializeLogger("\\PowerDisplay\\Logs");

            WinRT.ComWrappersSupport.InitializeComWrappers();

            // Parse command line arguments: args[0] = runner_pid (Awake pattern)
            int runnerPid = -1;

            if (args.Length >= 1)
            {
                if (int.TryParse(args[0], out int parsedPid))
                {
                    runnerPid = parsedPid;
                    Logger.LogInfo($"PowerDisplay started with runner_pid={runnerPid}");
                }
                else
                {
                    Logger.LogWarning($"Failed to parse PID from args[0]: {args[0]}");
                }
            }
            else
            {
                Logger.LogWarning("PowerDisplay started without runner PID. Running in standalone mode.");
            }

            var instanceKey = AppInstance.FindOrRegisterForKey("PowerToys_PowerDisplay_Instance");

            if (instanceKey.IsCurrent)
            {
                Microsoft.UI.Xaml.Application.Start((p) =>
                {
                    var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);
                    _ = new App(runnerPid);
                });
            }
            else
            {
                Logger.LogWarning("Another instance of PowerDisplay is running. Exiting.");
            }
        }
    }
}
