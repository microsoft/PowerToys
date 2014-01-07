using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Threading;

namespace WinAlfred
{
    public static class DispatcherExtensions
    {
        private static Dictionary<string, DispatcherTimer> timers =
            new Dictionary<string, DispatcherTimer>();
        private static readonly object syncRoot = new object();

        public static string DelayInvoke(this Dispatcher dispatcher, string namedInvocation,
            Action<string> action, TimeSpan delay,
            DispatcherPriority priority = DispatcherPriority.Normal)
        {
            return DelayInvoke(dispatcher, namedInvocation, action, delay, string.Empty, priority);
        }

        public static string DelayInvoke(this Dispatcher dispatcher, string namedInvocation,
            Action<string> action, TimeSpan delay, string arg,
            DispatcherPriority priority = DispatcherPriority.Normal)
        {
            lock (syncRoot)
            {
                if (String.IsNullOrEmpty(namedInvocation))
                {
                    namedInvocation = Guid.NewGuid().ToString();
                }
                else
                {
                    RemoveTimer(namedInvocation);
                }
                var timer = new DispatcherTimer(delay, priority, (s, e) =>
                {
                    RemoveTimer(namedInvocation);
                    action(arg);
                }, dispatcher);
                timer.Start();
                timers.Add(namedInvocation, timer);
                return namedInvocation;
            }
        }


        public static void CancelNamedInvocation(this Dispatcher dispatcher, string namedInvocation)
        {
            lock (syncRoot)
            {
                RemoveTimer(namedInvocation);
            }
        }

        private static void RemoveTimer(string namedInvocation)
        {
            if (!timers.ContainsKey(namedInvocation)) return;
            timers[namedInvocation].Stop();
            timers.Remove(namedInvocation);
        }

    }
}
