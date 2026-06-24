// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;

using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;

namespace MouseJump.WinUI3;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Logger.LogInfo("MouseJump process started");
        WinRT.ComWrappersSupport.InitializeComWrappers();

        var instanceKey = AppInstance.FindOrRegisterForKey("MouseJump_Instance");
        if (instanceKey.IsCurrent)
        {
            Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });
        }
        else
        {
            Logger.LogWarning("another instance is running. exiting");
        }

        return;
    }
}
