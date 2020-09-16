// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Windows;

namespace PowerLauncher.Helper
{
    public class NativeEventWaiter
    {
        public NativeEventWaiter(string eventName, Action callback)
        {
            const uint INFINITE = 0xFFFFFFFF;
            const uint WAIT_OBJECT_0 = 0x00000000;
            const uint SYNCHRONIZE = 0x00100000;

            IntPtr eventHandle = NativeMethods.OpenEventW(SYNCHRONIZE, false, eventName);

            new Thread(() =>
            {
                while (true)
                {
                    if (NativeMethods.WaitForSingleObject(eventHandle, INFINITE) == WAIT_OBJECT_0)
                    {
                        Application.Current.Dispatcher.Invoke(callback);
                    }
                }
            }).Start();
        }
    }
}
