#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

using IOPath = System.IO.Path;

namespace ImageResizer
{
    public sealed class TestDirectory : IDisposable
    {
        private readonly string _path;

        public TestDirectory()
        {
            _path = IOPath.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                IOPath.GetRandomFileName());
            Directory.CreateDirectory(_path);
        }

        private IEnumerable<string> Files
            => Directory.EnumerateFiles(_path);

        public IEnumerable<string> FileNames
            => Files.Select(IOPath.GetFileName);

        public string File() => Files.Single();

        public static implicit operator string(TestDirectory directory)
        {
            return directory?._path;
        }

        public override string ToString()
        {
            return _path;
        }

        public void Dispose()
        {
            // Try to delete the directory, with retries for file locks.
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
    }
}
