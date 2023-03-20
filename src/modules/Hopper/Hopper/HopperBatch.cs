// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Pipes;
using System.Text;

namespace Hopper
{
    internal sealed class HopperBatch
    {
        private readonly IFileSystem _fileSystem = new FileSystem();

        public string? DestinationDirectory { get; set; }

        public ICollection<string> Files { get; } = new List<string>();

        public static HopperBatch FromCommandLine(TextReader standardInput, string[] args)
        {
            var batch = new HopperBatch();
            const string pipeNamePrefix = "\\\\.\\pipe\\";
            string? pipeName = null;

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
                string? file;
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
                        string? file;

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
    }
}
