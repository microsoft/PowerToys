// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Windows;
using Microsoft.PowerToys.Common.Utils;

namespace ColorPicker.Helpers
{
    public static class NativeEventWaiter
    {
        private static Logger _logger;

        static NativeEventWaiter()
        {
            _logger = new Logger("ColorPicker\\Logs");
        }

        public static void WaitForEventLoop(string eventName, Action callback)
        {
            new Thread(() =>
            {
                var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
                while (true)
                {
                    if (eventHandle.WaitOne())
                    {
                        _logger.LogInfo($"Successfully waited for {eventName}");
                        Application.Current.Dispatcher.Invoke(callback);
                    }
                }
            }).Start();
        }
    }
}
