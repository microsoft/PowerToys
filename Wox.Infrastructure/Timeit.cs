using System;
using System.Collections.Generic;
using System.Diagnostics;
using Wox.Infrastructure.Logger;
using Wox.Plugin;

namespace Wox.Infrastructure
{
    public class Timeit : IDisposable
    {
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly string _name;

        public Timeit(string name)
        {
            _name = name;
            _stopwatch.Start();
        }

        public long Current
        {
            get
            {
                _stopwatch.Stop();
                long seconds = _stopwatch.ElapsedMilliseconds;
                _stopwatch.Start();
                string info = _name + " : " + _stopwatch.ElapsedMilliseconds + "ms";
                Debug.WriteLine(info);
                Log.Info(info);
                return seconds;
            }
        }


        public void Dispose()
        {
            _stopwatch.Stop();
            string info = _name + " : " + _stopwatch.ElapsedMilliseconds + "ms";
            Debug.WriteLine(info);
            Log.Info(info);
        }
    }
}
