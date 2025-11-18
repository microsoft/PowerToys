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

using ImageResizer.Properties;

namespace ImageResizer.Models
{
    public class ResizeBatch
    {
        private readonly IFileSystem _fileSystem = new FileSystem();

        public string DestinationDirectory { get; set; }

        public ICollection<string> Files { get; } = new List<string>();

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

        public IEnumerable<ResizeError> Process(Action<int, double> reportProgress, CancellationToken cancellationToken)
        {
            double total = Files.Count;
            int completed = 0;
            var errors = new ConcurrentBag<ResizeError>();

            // NOTE: Settings.Default is captured once before parallel processing.
            // Any changes to settings on disk during this batch will NOT be reflected until the next batch.
            // This improves performance and predictability by avoiding repeated mutex acquisition and behaviour change results in a batch.
            var settings = Settings.Default;

            // TODO: If we ever switch to Windows.Graphics.Imaging, we can get a lot more throughput by using the async
            //       APIs and a custom SynchronizationContext
            Parallel.ForEach(
                Files,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                },
                (file, state, i) =>
                {
                    try
                    {
                        Execute(file, settings);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(new ResizeError { File = _fileSystem.Path.GetFileName(file), Error = ex.Message });
                    }

                    Interlocked.Increment(ref completed);
                    reportProgress(completed, total);
                });

            return errors;
        }

        protected virtual void Execute(string file, Settings settings)
            => new ResizeOperation(file, DestinationDirectory, settings).Execute();
    }
}
