// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using IOPath = System.IO.Path;

namespace ImageResizer
{
    public class TestDirectory : IDisposable
    {
        private readonly string _path;
        private bool disposedValue;

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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    var stopwatch = Stopwatch.StartNew();
                    while (stopwatch.ElapsedMilliseconds < 30000)
                    {
                        try
                        {
                            Directory.Delete(_path, recursive: true);
                            break;
                        }
#pragma warning disable CA1031 // Do not catch general exception types
                        catch
#pragma warning restore CA1031 // Do not catch general exception types
                        {
                            Thread.Sleep(150);
                        }
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
