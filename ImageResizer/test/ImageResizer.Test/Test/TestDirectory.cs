using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;
using IOPath = System.IO.Path;

namespace ImageResizer
{
    public class TestDirectory : IDisposable
    {
        readonly string _path;

        public TestDirectory()
        {
            _path = IOPath.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                IOPath.GetRandomFileName());
            Directory.CreateDirectory(_path);
        }

        IEnumerable<string> Files
            => Directory.EnumerateFiles(_path);

        public IEnumerable<string> FileNames
            => Files.Select(IOPath.GetFileName);

        public string File()
            => Assert.Single(Files);

        public void Dispose()
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < 30000)
            {
                try
                {
                    Directory.Delete(_path, recursive: true);
                    break;
                }
                catch
                {
                    Thread.Sleep(150);
                }
            }
        }

        public static implicit operator string(TestDirectory directory)
            => directory._path;
    }
}