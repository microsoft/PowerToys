// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Windows;
using interop;

namespace ColorPicker.Helpers
{
    [Export(typeof(NativeEventWaiter))]
    public class NativeEventWaiter
    {
        private AppStateHandler _appStateHandler;

        [ImportingConstructor]
        public NativeEventWaiter(AppStateHandler appStateHandler)
        {
            _appStateHandler = appStateHandler;
            WaitForEventLoop(Constants.ShowColorPickerSharedEvent(), _appStateHandler.ShowColorPicker);
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
                        Logger.LogInfo("Successfully waited for SHOW_COLOR_PICKER_EVENT");
                        Application.Current.Dispatcher.Invoke(callback);
                    }
                }
            }).Start();
        }
    }
}
