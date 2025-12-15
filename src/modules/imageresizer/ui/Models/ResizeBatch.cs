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

        /// <summary>
        /// Creates a ResizeBatch from CliOptions.
        /// </summary>
        /// <param name="standardInput">Standard input stream for reading additional file paths.</param>
        /// <param name="options">The parsed CLI options.</param>
        /// <returns>A ResizeBatch instance.</returns>
        public static ResizeBatch FromCliOptions(TextReader standardInput, CliOptions options)
        {
            var batch = new ResizeBatch
            {
                DestinationDirectory = options.DestinationDirectory,
            };

            foreach (var file in options.Files)
            {
                // Convert relative paths to absolute paths
                var absolutePath = Path.IsPathRooted(file) ? file : Path.GetFullPath(file);
                batch.Files.Add(absolutePath);
            }

            if (string.IsNullOrEmpty(options.PipeName))
            {
                // NB: We read these from stdin since there are limits on the number of args you can have
                // Only read from stdin if it's redirected (piped input), not from interactive terminal
                string file;
                if (standardInput != null && Console.IsInputRedirected)
                {
                    while ((file = standardInput.ReadLine()) != null)
                    {
                        // Convert relative paths to absolute paths
                        var absolutePath = Path.IsPathRooted(file) ? file : Path.GetFullPath(file);
                        batch.Files.Add(absolutePath);
                    }
                }
            }
            else
            {
                using (NamedPipeClientStream pipeClient =
                    new NamedPipeClientStream(".", options.PipeName, PipeDirection.In))
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

        public static ResizeBatch FromCommandLine(TextReader standardInput, string[] args)
        {
            var options = CliOptions.Parse(args);
            return FromCliOptions(standardInput, options);
        }

        public IEnumerable<ResizeError> Process(Action<int, double> reportProgress, CancellationToken cancellationToken)
        {
            // NOTE: Settings.Default is captured once before parallel processing.
            // Any changes to settings on disk during this batch will NOT be reflected until the next batch.
            // This improves performance and predictability by avoiding repeated mutex acquisition and behaviour change results in a batch.
            return Process(reportProgress, Settings.Default, cancellationToken);
        }

        public IEnumerable<ResizeError> Process(Action<int, double> reportProgress, Settings settings, CancellationToken cancellationToken)
        {
            double total = Files.Count;
            int completed = 0;
            var errors = new ConcurrentBag<ResizeError>();

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
