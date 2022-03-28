using System;
using System.Threading;
using System.Windows;

namespace PeekUI.Native
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
                        Application.Current.Dispatcher.Invoke(callback);
                    }
                }
            }).Start();
        }
    }
}