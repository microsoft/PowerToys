// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Wox.Plugin.Logger;

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
            ArgumentNullException.ThrowIfNull(action);

            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            action();
            stopWatch.Stop();
            var milliseconds = stopWatch.ElapsedMilliseconds;
            string info = $"{message} <{milliseconds}ms>";
            Log.Debug(info, MethodBase.GetCurrentMethod().DeclaringType);
            return milliseconds;
        }

        public static long Normal(string message, Action action)
        {
            ArgumentNullException.ThrowIfNull(action);

            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            action();
            stopWatch.Stop();
            var milliseconds = stopWatch.ElapsedMilliseconds;
            string info = $"{message} <{milliseconds}ms>";
            Log.Info(info, MethodBase.GetCurrentMethod().DeclaringType);
            return milliseconds;
        }

        public static void StartCount(string name, Action action)
        {
            ArgumentNullException.ThrowIfNull(action);

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
                Log.Debug(info, MethodBase.GetCurrentMethod().DeclaringType);
            }
        }
    }
}
