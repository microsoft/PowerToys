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

            // Parse command line arguments: args[0] = runner_pid, args[1] = pipe_uuid
            int runnerPid = -1;
            string pipeUuid = string.Empty;

            if (args.Length >= 2)
            {
                if (int.TryParse(args[0], out int parsedPid))
                {
                    runnerPid = parsedPid;
                }
                pipeUuid = args[1];
                Logger.LogInfo($"PowerDisplay started with runner_pid={runnerPid}, pipe_uuid={pipeUuid}");
            }
            else
            {
                Logger.LogWarning("PowerDisplay started without command line arguments");
                Logger.LogWarning($"PowerDisplay started with insufficient arguments (expected 2, got {args.Length}). Running in standalone mode.");
            }

            var instanceKey = AppInstance.FindOrRegisterForKey("PowerToys_PowerDisplay_Instance");

            if (instanceKey.IsCurrent)
            {
                Microsoft.UI.Xaml.Application.Start((p) =>
                {
                    var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);
                    _ = new App(runnerPid, pipeUuid);
                });
            }
            else
            {
                Logger.LogWarning("Another instance of PowerDisplay is running. Exiting.");
            }
        }
    }
}
