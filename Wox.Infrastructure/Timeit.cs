using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                Debug.WriteLine(_name + ":" + _stopwatch.ElapsedMilliseconds + "ms");
                return seconds;
            }
        }


        public void Dispose()
        {
            _stopwatch.Stop();
            Debug.WriteLine(_name + ":" + _stopwatch.ElapsedMilliseconds + "ms");
        }
    }
}
