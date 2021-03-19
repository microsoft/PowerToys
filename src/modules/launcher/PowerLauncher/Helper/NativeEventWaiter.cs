// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Threading;
using System.Windows;
using Wox.Plugin.Logger;

namespace PowerLauncher.Helper
{
    public static class NativeEventWaiter
    {
        public static void WaitForEventLoop(string eventName, Action callback)
        {
            new Thread(() =>
            {
                var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
                while (true)
                {
                    if (eventHandle.WaitOne())
                    {
                        Log.Info($"Successfully waited for {eventName}", MethodBase.GetCurrentMethod().DeclaringType);
                        Application.Current.Dispatcher.Invoke(callback);
                    }
                }
            }).Start();
        }
    }
}
