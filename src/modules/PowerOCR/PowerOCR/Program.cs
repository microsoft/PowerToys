// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace PowerOCR;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        Logger.InitializeLogger("\\TextExtractor\\Logs");
        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredTextExtractorEnabledValue()
            == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
        {
            Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
            return 0;
        }

        using var instanceMutex = new Mutex(
            true,
            @"Local\PowerToys_PowerOCR_InstanceMutex",
            out bool createdNew);
        if (!createdNew)
        {
            Logger.LogWarning("Another running TextExtractor instance was detected. Exiting TextExtractor");
            return 0;
        }

        int runnerPid = args.Length > 0 && int.TryParse(args[0], out int parsedPid) ? parsedPid : -1;

        try
        {
            Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App(runnerPid);
            });
            return 0;
        }
        finally
        {
            instanceMutex.ReleaseMutex();
        }
    }
}
