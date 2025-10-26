#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImageResizer.Models.ResizeResults;
using ImageResizer.Properties;

namespace ImageResizer.Models
{
    public class ResizeBatch
    {
        private readonly IFileSystem _fileSystem = new FileSystem();

        public virtual string DestinationDirectory { get; set; }

        public virtual ICollection<string> Files { get; } = new List<string>();

        public static ResizeBatch FromCommandLine(TextReader standardInput, string[] args)
        {
            var batch = new ResizeBatch();
            const string pipeNamePrefix = "\\\\.\\pipe\\";
            string pipeName = null;

            for (var i = 0; i < args?.Length; i++)
            {
                if (args[i] == "/d")
                {
                    batch.DestinationDirectory = args[++i];
                    continue;
                }
                else if (args[i].Contains(pipeNamePrefix))
                {
                    pipeName = args[i].Substring(pipeNamePrefix.Length);
                    continue;
                }

                batch.Files.Add(args[i]);
            }

            if (string.IsNullOrEmpty(pipeName))
            {
                // NB: We read these from stdin since there are limits on the number of args you can have
                string file;
                if (standardInput != null)
                {
                    while ((file = standardInput.ReadLine()) != null)
                    {
                        batch.Files.Add(file);
                    }
                }
            }
            else
            {
                using (NamedPipeClientStream pipeClient =
                    new NamedPipeClientStream(".", pipeName, PipeDirection.In))
                {
                    // Connect to the pipe or wait until the pipe is available.
                    pipeClient.Connect();

                    using (StreamReader sr = new StreamReader(pipeClient, Encoding.Unicode))
                    {
                        string file;

                        // Display the read text to the console
                        while ((file = sr.ReadLine()) != null)
                        {
                            batch.Files.Add(file);
                        }
                    }
                }
            }

            return batch;
        }

        public IEnumerable<ResizeResult> Process(Action<int, double> reportProgress, CancellationToken cancellationToken)
        {
            double total = Files.Count;
            int completed = 0;
            ConcurrentBag<ResizeResult> results = [];

            // TODO: If we ever switch to Windows.Graphics.Imaging, we can get a lot more throughput by using the async
            //       APIs and a custom SynchronizationContext
            Parallel.ForEach(
                Files,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                },
                (file, state, i) =>
                {
                    var result = Execute(file);
                    results.Add(result);

                    Interlocked.Increment(ref completed);

                    reportProgress(completed, total);
                });

            return results;
        }

        protected virtual ResizeResult Execute(string file)
            => new ResizeOperation(file, DestinationDirectory, Settings.Default).Execute();
    }
}
