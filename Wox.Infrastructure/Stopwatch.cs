using System;
using System.Diagnostics;
using Wox.Infrastructure.Logger;

namespace Wox.Infrastructure
{
    public static class Stopwatch
    {
        /// <summary>
        /// This stopwatch will appear only in Debug mode
        /// </summary>
        public static void Debug(string name, Action action)
        {
#if DEBUG
            Normal(name, action);
#else
            action();
#endif
        }

        [Conditional("DEBUG")]
        private static void WriteTimeInfo(string name, long milliseconds)
        {
            string info = $"{name} : {milliseconds}ms";
            System.Diagnostics.Debug.WriteLine(info);
            Log.Info(info);
        }

        /// <summary>
        /// This stopwatch will also appear only in Debug mode
        /// </summary>
        public static long Normal(string name, Action action)
        {
            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            action();
            stopWatch.Stop();
            var milliseconds = stopWatch.ElapsedMilliseconds;
            WriteTimeInfo(name, milliseconds);
            return milliseconds;
        }

    }
}
