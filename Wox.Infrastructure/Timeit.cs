using System;
using System.Diagnostics;

namespace Wox.Infrastructure
{
    public class Timeit : IDisposable
    {
        private Stopwatch stopwatch = new Stopwatch();
        private string name;

        public Timeit(string name)
        {
            this.name = name;
            stopwatch.Start();
        }

        public void Dispose()
        {
            stopwatch.Stop();
            Debug.WriteLine(name + ":" + stopwatch.ElapsedMilliseconds + "ms");
        }
    }
}
