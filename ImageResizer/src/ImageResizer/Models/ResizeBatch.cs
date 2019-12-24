using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageResizer.Properties;

namespace ImageResizer.Models
{
    public class ResizeBatch
    {
        public string DestinationDirectory { get; set; }
        public ICollection<string> Files { get; } = new List<string>();

        public static ResizeBatch FromCommandLine(TextReader standardInput, string[] args)
        {
            var batch = new ResizeBatch();

            // NB: We read these from stdin since there are limits on the number of args you can have
            string file;
            while ((file = standardInput.ReadLine()) != null)
                batch.Files.Add(file);

            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == "/d")
                {
                    batch.DestinationDirectory = args[++i];
                    continue;
                }

                batch.Files.Add(args[i]);
            }

            return batch;
        }

        public IEnumerable<ResizeError> Process(
            CancellationToken cancellationToken,
            Action<int, double> reportProgress)
        {
            double total = Files.Count;
            var completed = 0;
            var errors = new ConcurrentBag<ResizeError>();

            // TODO: If we ever switch to Windows.Graphics.Imaging, we can get a lot more throughput by using the async
            //       APIs and a custom SynchronizationContext
            Parallel.ForEach(
                Files,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                },
                (file, state, i) =>
                {
                    try
                    {
                        Execute(file);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(new ResizeError { File = Path.GetFileName(file), Error = ex.Message });
                    }

                    Interlocked.Increment(ref completed);

                    reportProgress(completed, total);
                });

            return errors;
        }

        protected virtual void Execute(string file)
            => new ResizeOperation(file, DestinationDirectory, Settings.Default).Execute();
    }
}
