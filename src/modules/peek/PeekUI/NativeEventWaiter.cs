using Microsoft.UI.Dispatching;
using System;
using System.Threading;

namespace PeekUI
{
    public static class NativeEventWaiter
    {
        public static void WaitForEventLoop(string eventName, Action callback, DispatcherQueue dispatcherQueue)
        {
            new Thread(() =>
            {
                var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
                while (true)
                {
                    if (eventHandle.WaitOne())
                    {
                        // Logger.LogInfo($"Successfully waited for {eventName}");
                        dispatcherQueue.TryEnqueue(() => callback.Invoke());
                    }
                }
            }).Start();
        }
    }
}
