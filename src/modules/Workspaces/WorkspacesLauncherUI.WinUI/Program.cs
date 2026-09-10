// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

using ManagedCommon;
using Microsoft.UI.Dispatching;

namespace WorkspacesLauncherUI
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Logger.InitializeLogger("\\Workspaces\\WorkspacesLauncherUI");

            WinRT.ComWrappersSupport.InitializeComWrappers();

            if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredWorkspacesEnabledValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
                return;
            }

            const string mutexName = "Local\\PowerToys_Workspaces_LauncherUI_InstanceMutex";
            bool createdNew;
            using var mutex = new Mutex(true, mutexName, out createdNew);

            if (!createdNew)
            {
                Logger.LogWarning("Another instance of Workspaces Launcher UI is already running. Exiting this instance.");
                return;
            }

            Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });
        }
    }
}
