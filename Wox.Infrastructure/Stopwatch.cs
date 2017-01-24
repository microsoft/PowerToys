using System;
using System.Collections.Generic;
using Wox.Infrastructure.Logger;

namespace Wox.Infrastructure
{
    public static class Stopwatch
    {
        private static readonly Dictionary<string, long> Count = new Dictionary<string, long>();
        private static readonly object Locker = new object();
        /// <summary>
        /// This stopwatch will appear only in Debug mode
        /// </summary>
        public static long Debug(string message, Action action)
        {
            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            action();
            stopWatch.Stop();
            var milliseconds = stopWatch.ElapsedMilliseconds;
            string info = $"{message} <{milliseconds}ms>";
            Log.Debug(info);
            return milliseconds;
        }

        public static long Normal(string message, Action action)
        {
            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            action();
            stopWatch.Stop();
            var milliseconds = stopWatch.ElapsedMilliseconds;
            string info = $"{message} <{milliseconds}ms>";
            Log.Info(info);
            return milliseconds;
        }

        public static void StartCount(string name, Action action)
        {
            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            action();
            stopWatch.Stop();
            var milliseconds = stopWatch.ElapsedMilliseconds;
            lock (Locker)
            {
                if (Count.ContainsKey(name))
                {
                    Count[name] += milliseconds;
                }
                else
                {
                    Count[name] = 0;
                }
            }
        }

        public static void EndCount()
        {
            foreach (var key in Count.Keys)
            {
                string info = $"{key} already cost {Count[key]}ms";
                Log.Debug(info);
            }
        }
    }
}
